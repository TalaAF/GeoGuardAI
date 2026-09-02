using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AeroCompliance.Contracts.Messaging;
using AeroCompliance.Contracts.Messaging.Messages;
using AeroCompliance.Domain.Entities;
using AeroCompliance.Domain.Enums;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AeroCompliance.ProcessingService;

public sealed class AnalysisResultConsumer(IConnection connection, IServiceScopeFactory scopes, ILogger<AnalysisResultConsumer> logger) : BackgroundService
{
    private const string ConsumerName = "AeroCompliance.ProcessingService";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.ExchangeDeclareAsync(MessagingConstants.ProcessingExchange, ExchangeType.Topic, true, false, cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(MessagingConstants.ProcessingResultsDeadLetterQueue, true, false, false, cancellationToken: stoppingToken);
        await channel.QueueBindAsync(MessagingConstants.ProcessingResultsDeadLetterQueue, MessagingConstants.ProcessingExchange, "image.analysis.dead.v1", cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(MessagingConstants.ProcessingResultsQueue, true, false, false,
            new Dictionary<string, object?> { ["x-dead-letter-exchange"] = MessagingConstants.ProcessingExchange, ["x-dead-letter-routing-key"] = "image.analysis.dead.v1" }, cancellationToken: stoppingToken);
        foreach (var key in new[] { MessagingConstants.AnalysisStartedRoutingKey, MessagingConstants.AnalysisCompletedRoutingKey, MessagingConstants.AnalysisFailedRoutingKey })
            await channel.QueueBindAsync(MessagingConstants.ProcessingResultsQueue, MessagingConstants.ProcessingExchange, key, cancellationToken: stoppingToken);
        await channel.BasicQosAsync(0, 4, false, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try { await HandleAsync(ea, stoppingToken); await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Failed processing AI result {MessageId}", ea.BasicProperties.MessageId); await channel.BasicNackAsync(ea.DeliveryTag, false, !ea.Redelivered, stoppingToken); }
        };
        await channel.BasicConsumeAsync(MessagingConstants.ProcessingResultsQueue, false, consumer, stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleAsync(BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var body = ea.Body.ToArray(); using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AeroComplianceDbContext>();
        using var doc = JsonDocument.Parse(body); var eventId = doc.RootElement.GetProperty("eventId").GetGuid();
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            if (await db.InboxMessages.AnyAsync(x => x.EventId == eventId && x.Consumer == ConsumerName, ct)) return;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var inbox = new InboxMessage { EventId = eventId, Consumer = ConsumerName, EventType = ea.RoutingKey,
                PayloadHash = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant(), ReceivedAt = DateTimeOffset.UtcNow };
            db.InboxMessages.Add(inbox);
            switch (ea.RoutingKey)
            {
                case MessagingConstants.AnalysisStartedRoutingKey: await Started(db, JsonSerializer.Deserialize<ImageAnalysisStarted>(body, Json)!, ct); break;
                case MessagingConstants.AnalysisCompletedRoutingKey: await Completed(db, JsonSerializer.Deserialize<ImageAnalysisCompleted>(body, Json)!, ct); break;
                case MessagingConstants.AnalysisFailedRoutingKey: await Failed(db, JsonSerializer.Deserialize<ImageAnalysisFailed>(body, Json)!, ct); break;
                default: throw new InvalidDataException("Unknown analysis event.");
            }
            inbox.CompletedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        });
    }

    private static async Task Started(AeroComplianceDbContext db, ImageAnalysisStarted e, CancellationToken ct)
    {
        var image = await db.Images.SingleAsync(x => x.Id == e.ImageId, ct); if (image.ProcessingRequestId != e.RequestId) return;
        image.ProcessingStatus = ImageProcessingStatus.Analyzing; image.ProcessingStartedAt = e.StartedAt;
        if (!await db.ImageAnalyses.AnyAsync(x => x.RequestId == e.RequestId, ct)) db.ImageAnalyses.Add(new ImageAnalysis { Id = Guid.NewGuid(), ImageId = e.ImageId,
            RequestId = e.RequestId, Mode = e.Mode, ModelName = e.ModelName, ModelVersion = e.ModelVersion, ConfidenceThreshold = e.ConfidenceThreshold,
            OutputCrs = "EPSG:4326", Status = "Analyzing", AttemptNumber = e.Attempt, StartedAt = e.StartedAt });
    }

    private static async Task Completed(AeroComplianceDbContext db, ImageAnalysisCompleted e, CancellationToken ct)
    {
        var image = await db.Images.Include(x => x.Flight).SingleAsync(x => x.Id == e.ImageId, ct); if (image.ProcessingRequestId != e.RequestId) return;
        var analysis = await db.ImageAnalyses.SingleOrDefaultAsync(x => x.RequestId == e.RequestId, ct) ?? new ImageAnalysis { Id = Guid.NewGuid(), ImageId = e.ImageId, RequestId = e.RequestId };
        if (db.Entry(analysis).State == EntityState.Detached) db.ImageAnalyses.Add(analysis);
        analysis.Mode=e.Mode;analysis.ModelName=e.ModelName;analysis.ModelVersion=e.ModelVersion;analysis.ConfidenceThreshold=e.ConfidenceThreshold;analysis.SourceCrs=e.SourceCrs;
        analysis.OutputCrs=e.OutputCrs;analysis.Status="EvaluatingCompliance";analysis.AttemptNumber=e.Attempt;analysis.StartedAt=e.StartedAt;analysis.CompletedAt=e.CompletedAt;analysis.DurationMs=e.DurationMs;
        image.ProcessingStatus=ImageProcessingStatus.EvaluatingCompliance; await db.SaveChangesAsync(ct);
        foreach (var item in e.Detections)
        {
            if (await db.Detections.AnyAsync(x => x.ImageAnalysisId == analysis.Id && x.ExternalDetectionId == item.DetectionId, ct)) continue;
            var geom=GeometryReader.Read(item.Geometry); var detection=new Detection { Id=Guid.NewGuid(),ImageAnalysisId=analysis.Id,ExternalDetectionId=item.DetectionId,ImageId=image.Id,
                ClassId=item.ClassId,ClassLabel=item.ClassName,Confidence=item.Confidence,Geom=geom,ModelName=e.ModelName,ModelVersion=e.ModelVersion,CreatedAt=e.CompletedAt };
            db.Detections.Add(detection); await db.SaveChangesAsync(ct); await Evaluate(db, image.Flight.MunicipalityId, analysis, detection, ct);
        }
        analysis.Status="Completed";image.ProcessingStatus=ImageProcessingStatus.Completed;image.ProcessingCompletedAt=e.CompletedAt;image.LastErrorCode=null;image.LastErrorMessage=null;
        db.AuditLogs.Add(new AuditLog { Action="image.processing.completed",EntityType="Image",EntityId=image.Id,MunicipalityId=image.Flight.MunicipalityId,
            AfterState=JsonSerializer.Serialize(new{analysisId=analysis.Id,detections=e.Detections.Count}),CreatedAt=DateTimeOffset.UtcNow });
    }

    private static async Task Failed(AeroComplianceDbContext db, ImageAnalysisFailed e, CancellationToken ct)
    {
        var image=await db.Images.SingleAsync(x=>x.Id==e.ImageId,ct);if(image.ProcessingRequestId!=e.RequestId)return;
        image.ProcessingStatus=ImageProcessingStatus.AnalysisFailed;image.ProcessingCompletedAt=e.FailedAt;image.LastErrorCode=e.ErrorCode;image.LastErrorMessage=e.Message[..Math.Min(1000,e.Message.Length)];
        var analysis=await db.ImageAnalyses.SingleOrDefaultAsync(x=>x.RequestId==e.RequestId,ct);if(analysis is not null){analysis.Status="Failed";analysis.CompletedAt=e.FailedAt;analysis.FailureCode=e.ErrorCode;analysis.FailureMessage=image.LastErrorMessage;}
    }

    private static async Task Evaluate(AeroComplianceDbContext db, Guid municipalityId, ImageAnalysis analysis, Detection detection, CancellationToken ct)
    {
        if (detection.ClassLabel is not ("building" or "violation")) return;
        var conn=(NpgsqlConnection)db.Database.GetDbConnection();
        var road=await SpatialHit(conn,"SELECT id, ST_Area(ST_Intersection(ST_Transform(geom,32636),ST_Transform(@g,32636))) FROM roads WHERE municipality_id=@m AND ST_Intersects(geom,@g) ORDER BY 2 DESC LIMIT 1",municipalityId,detection.Geom,ct);
        if(road is not null){await AddViolation(db,analysis,detection,"road_encroachment",road.Value.Id,null,null,road.Value.Value,ct);return;}
        var parcel=await SpatialHit(conn,"SELECT p.id, ST_Area(ST_Difference(ST_Transform(@g,32636),ST_Transform(p.geom,32636))) FROM parcels p JOIN blocks b ON p.block_id=b.id JOIN zones z ON b.zone_id=z.id WHERE z.municipality_id=@m AND ST_Intersects(p.geom,@g) ORDER BY ST_Area(ST_Intersection(ST_Transform(p.geom,32636),ST_Transform(@g,32636))) DESC LIMIT 1",municipalityId,detection.Geom,ct);
        if(parcel is not null&&parcel.Value.Value>0.01m)await AddViolation(db,analysis,detection,"parcel_encroachment",null,parcel.Value.Id,null,parcel.Value.Value,ct);
    }
    private static async Task<(Guid Id,decimal Value)?> SpatialHit(NpgsqlConnection conn,string sql,Guid municipalityId,NetTopologySuite.Geometries.MultiPolygon geom,CancellationToken ct){await using var cmd=new NpgsqlCommand(sql,conn);cmd.Parameters.AddWithValue("m",municipalityId);cmd.Parameters.AddWithValue("g",NpgsqlDbType.Geometry,geom);await using var reader=await cmd.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct))return null;return(reader.GetGuid(0),reader.IsDBNull(1)?0:Convert.ToDecimal(reader.GetDouble(1)));}
    private static async Task AddViolation(AeroComplianceDbContext db,ImageAnalysis analysis,Detection detection,string type,Guid? roadId,Guid? parcelId,Guid? ruleId,decimal measured,CancellationToken ct){if(await db.ComplianceEvaluations.AnyAsync(x=>x.DetectionId==detection.Id&&x.EvaluationType==type,ct))return;var evaluation=new ComplianceEvaluation{Id=Guid.NewGuid(),ImageAnalysisId=analysis.Id,DetectionId=detection.Id,ZoningRuleId=ruleId,EvaluationType=type,MeasuredValue=measured,Unit="m2",Outcome="candidate_violation",MetricCrs=32636,CreatedAt=DateTimeOffset.UtcNow};db.ComplianceEvaluations.Add(evaluation);var violation=new Violation{Id=Guid.NewGuid(),ComplianceEvaluationId=evaluation.Id,DetectionId=detection.Id,ParcelId=parcelId,RoadId=roadId,RuleId=ruleId,ViolationType=type,ExceedanceAreaM2=measured,AiConfidenceScore=detection.Confidence,CurrentStatusId=1,Geom=detection.Geom,CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow};db.Violations.Add(violation);db.ViolationStatusHistory.Add(new ViolationStatusHistory{Id=Guid.NewGuid(),ViolationId=violation.Id,StatusId=1,Notes="Created by automated compliance evaluation.",ChangedAt=DateTimeOffset.UtcNow});}
}
