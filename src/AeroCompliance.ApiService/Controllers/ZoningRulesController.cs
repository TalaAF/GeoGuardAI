using AeroCompliance.ApiService.Authorization; using AeroCompliance.ApiService.Services; using AeroCompliance.Contracts.Dtos.Common; using AeroCompliance.Contracts.Dtos.Gis; using AeroCompliance.Domain.Entities; using AeroCompliance.Infrastructure.Compliance; using AeroCompliance.Infrastructure.Persistence; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
namespace AeroCompliance.ApiService.Controllers;
[ApiController,Route("api/zoning-rules"),Authorize] public sealed class ZoningRulesController(AeroComplianceDbContext db,ICurrentUserContext user,IAuditService audit):ControllerBase
{
 [HttpGet] public async Task<ActionResult<PagedResponse<ZoningRuleDto>>> List(int page=1,int pageSize=25,Guid? municipalityId=null,Guid? zoneId=null,string? search=null,bool? isActive=null,bool? isValidated=null,CancellationToken ct=default){if(!ApiSupport.IsValidPagination(page,pageSize))return Problem(statusCode:400,title:"Invalid pagination");var q=Visible().AsNoTracking();if(user.IsPlatformAdmin&&municipalityId.HasValue)q=q.Where(x=>x.MunicipalityId==municipalityId);if(zoneId.HasValue)q=q.Where(x=>x.ZoneId==zoneId);var s=ApiSupport.NormalizeSearch(search);if(s is not null)q=q.Where(x=>x.RuleKey.ToLower().Contains(s)||(x.ZoneType!=null&&x.ZoneType.ToLower().Contains(s)));if(isActive.HasValue)q=q.Where(x=>x.IsActive==isActive);if(isValidated.HasValue)q=q.Where(x=>x.IsValidated==isValidated);var paged=await q.OrderBy(x=>x.RuleKey).Select(MapExpr()).ToPagedAsync(page,pageSize,ct);return Ok(paged with{Items=paged.Items.Select(Decorate).ToList()});}
 [HttpGet("{id:guid}")] public async Task<ActionResult<ZoningRuleDto>> Get(Guid id,CancellationToken ct){var x=await Visible().AsNoTracking().Where(x=>x.Id==id).Select(MapExpr()).SingleOrDefaultAsync(ct);return x is null?NotFound():Ok(Decorate(x));}
 [HttpPost,Authorize(Policy=Policies.ManageGis)] public async Task<ActionResult<ZoningRuleDto>> Create(SaveZoningRuleRequest r,CancellationToken ct){if(!ValidTarget(r))return Problem(statusCode:400,title:"Invalid rule target",detail:"Specify exactly one of zoneId or zoneType.");if(ValidateAgainstCatalog(r,this) is ActionResult invalid)return invalid;var tenant=await ResolveTenant(r,ct);if(tenant is null)return NotFound();var x=new ZoningRule{Id=Guid.NewGuid(),MunicipalityId=tenant.Value,ZoneId=r.ZoneId,ZoneType=r.ZoneType?.Trim(),RuleKey=ZoningRuleCatalog.Find(r.RuleKey)!.CanonicalKey,RuleValue=r.RuleValue,Unit=r.Unit.Trim().ToLowerInvariant(),SourceDocument=r.SourceDocument?.Trim(),ExtractedByAi=r.ExtractedByAi,IsValidated=false,IsActive=r.IsActive,CreatedAt=DateTimeOffset.UtcNow};db.ZoningRules.Add(x);audit.Add("zoning_rule.create","ZoningRule",x.Id,after:new{x.MunicipalityId,x.ZoneId,x.ZoneType,x.RuleKey,x.RuleValue,x.Unit,x.IsActive});await db.SaveChangesAsync(ct);return CreatedAtAction(nameof(Get),new{id=x.Id},await Reload(x.Id,ct));}
 [HttpPut("{id:guid}"),Authorize(Policy=Policies.ManageGis)] public async Task<ActionResult<ZoningRuleDto>> Update(Guid id,SaveZoningRuleRequest r,CancellationToken ct){var x=await Visible().SingleOrDefaultAsync(x=>x.Id==id,ct);if(x is null)return NotFound();if(!ValidTarget(r))return Problem(statusCode:400,title:"Invalid rule target",detail:"Specify exactly one of zoneId or zoneType.");if(ValidateAgainstCatalog(r,this) is ActionResult invalid)return invalid;if(r.ZoneId.HasValue&&!await db.Zones.AnyAsync(z=>z.Id==r.ZoneId&&z.MunicipalityId==x.MunicipalityId,ct))return NotFound();if(user.IsPlatformAdmin&&r.MunicipalityId.HasValue&&r.MunicipalityId!=x.MunicipalityId)return Problem(statusCode:409,title:"Conflict",detail:"A rule cannot be moved between municipalities.");var before=new{x.ZoneId,x.ZoneType,x.RuleKey,x.RuleValue,x.Unit,x.SourceDocument,x.ExtractedByAi,x.IsActive,x.IsValidated};x.ZoneId=r.ZoneId;x.ZoneType=r.ZoneType?.Trim();x.RuleKey=ZoningRuleCatalog.Find(r.RuleKey)!.CanonicalKey;x.RuleValue=r.RuleValue;x.Unit=r.Unit.Trim().ToLowerInvariant();x.SourceDocument=r.SourceDocument?.Trim();x.ExtractedByAi=r.ExtractedByAi;x.IsActive=r.IsActive;x.IsValidated=false;x.ValidatedBy=null;x.ValidatedAt=null;audit.Add("zoning_rule.update","ZoningRule",id,before,new{x.ZoneId,x.ZoneType,x.RuleKey,x.RuleValue,x.Unit,x.SourceDocument,x.ExtractedByAi,x.IsActive,x.IsValidated});await db.SaveChangesAsync(ct);return Ok(await Reload(id,ct));}
 [HttpPost("{id:guid}/validate"),Authorize(Policy=Policies.AnalyzeGis)] public async Task<ActionResult<ZoningRuleDto>> Validate(Guid id,CancellationToken ct){var x=await Visible().SingleOrDefaultAsync(x=>x.Id==id,ct);if(x is null)return NotFound();if(user.UserId is not Guid actor)return Unauthorized();var before=new{x.IsValidated,x.ValidatedBy,x.ValidatedAt};x.IsValidated=true;x.ValidatedBy=actor;x.ValidatedAt=DateTimeOffset.UtcNow;audit.Add("zoning_rule.validate","ZoningRule",id,before,new{x.IsValidated,x.ValidatedBy,x.ValidatedAt});await db.SaveChangesAsync(ct);return Ok(await Reload(id,ct));}
 private IQueryable<ZoningRule> Visible()=>user.IsPlatformAdmin?db.ZoningRules:db.ZoningRules.Where(x=>x.MunicipalityId==user.MunicipalityId);private static bool ValidTarget(SaveZoningRuleRequest r)=>r.ZoneId.HasValue^!string.IsNullOrWhiteSpace(r.ZoneType);private async Task<Guid?> ResolveTenant(SaveZoningRuleRequest r,CancellationToken ct){var tenant=user.IsPlatformAdmin?r.MunicipalityId:user.MunicipalityId;
  // A platform admin who names a zone has already identified the tenant; derive it rather than
  // rejecting the request for a municipalityId the zone itself determines.
  if(tenant is null&&user.IsPlatformAdmin&&r.ZoneId.HasValue)
   tenant=await db.Zones.Where(x=>x.Id==r.ZoneId).Select(x=>(Guid?)x.MunicipalityId).SingleOrDefaultAsync(ct);
  if(tenant is null)return null;if(r.ZoneId.HasValue)return await db.Zones.AnyAsync(x=>x.Id==r.ZoneId&&x.MunicipalityId==tenant,ct)?tenant:null;return await db.Municipalities.AnyAsync(x=>x.Id==tenant,ct)?tenant:null;}
 // Re-reads through the same tenant-scoped, name-projecting query used by GET, so a mutation's response
 // always reflects the just-saved Zone/Validator names rather than stale/unloaded navigation properties.
 private async Task<ZoningRuleDto> Reload(Guid id,CancellationToken ct)=>Decorate(await Visible().AsNoTracking().Where(x=>x.Id==id).Select(MapExpr()).SingleAsync(ct));
 private static System.Linq.Expressions.Expression<Func<ZoningRule,ZoningRuleDto>> MapExpr()=>x=>new(x.Id,x.MunicipalityId,x.ZoneId,x.Zone!=null?x.Zone.ZoneNumber:null,x.ZoneType,x.RuleKey,x.RuleValue,x.Unit,x.SourceDocument,x.ExtractedByAi,x.IsValidated,x.ValidatedBy,x.Validator!=null?x.Validator.FullName:null,x.ValidatedAt,x.IsActive,x.CreatedAt);

 /// <summary>
 /// Adds the catalogue-derived display fields. Kept out of <see cref="MapExpr"/> because the
 /// catalogue is plain C# and cannot be translated to SQL; the projection runs first, this second.
 /// </summary>
 private static ZoningRuleDto Decorate(ZoningRuleDto dto)
 {
  var definition=ZoningRuleCatalog.Find(dto.RuleKey);
  if(definition is null)
   return dto with{RuleLabel=dto.RuleKey.Replace('_',' '),RuleDescription=null,
    EnforcementState=EnforcementStates.Unsupported,RequirementDisplay=$"{dto.RuleValue:0.##} {dto.Unit}",IsSupported=false};
  var probe=new ZoningRule{RuleKey=dto.RuleKey,RuleValue=dto.RuleValue,Unit=dto.Unit,IsActive=dto.IsActive,IsValidated=dto.IsValidated};
  return dto with{RuleLabel=definition.Label,RuleDescription=definition.Description,
   EnforcementState=ZoningRuleCatalog.EnforcementState(probe),
   RequirementDisplay=definition.Format(dto.RuleValue),IsSupported=true};
 }

 /// <summary>
 /// Rejects a rule key the engine cannot execute. Storing an unrunnable rule would show up as
 /// "Unsupported" and silently never enforce, so manual entry is constrained to the catalogue.
 /// </summary>
 private static ActionResult? ValidateAgainstCatalog(SaveZoningRuleRequest r,ControllerBase c)
 {
  var definition=ZoningRuleCatalog.Find(r.RuleKey);
  if(definition is null)
   return c.Problem(statusCode:400,title:"Unsupported rule type",
    detail:$"'{r.RuleKey}' is not a rule this system can enforce. Supported types: "+
     string.Join(", ",ZoningRuleCatalog.All.Select(d=>$"{d.Label} ({d.CanonicalKey})"))+".");
  if(!string.Equals(r.Unit?.Trim(),definition.Unit,StringComparison.OrdinalIgnoreCase))
   return c.Problem(statusCode:400,title:"Incorrect unit",
    detail:$"{definition.Label} is measured in {definition.UnitLabel}, so the unit must be '{definition.Unit}'.");
  if(r.RuleValue<definition.MinimumValue||r.RuleValue>definition.MaximumValue)
   return c.Problem(statusCode:400,title:"Value out of range",
    detail:$"{definition.Label} must be between {definition.Format(definition.MinimumValue)} and {definition.Format(definition.MaximumValue)}.");
  return null;
 }
}
