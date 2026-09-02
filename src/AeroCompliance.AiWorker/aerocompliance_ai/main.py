import hashlib, json, os, tempfile, threading, time, uuid
from datetime import datetime, timezone
from flask import Flask, jsonify
from minio import Minio
import pika
from .config import Settings
from .model import MockModelRunner, YoloModelRunner, ModelFailure

EXCHANGE="aerocompliance.processing"; REQUEST_KEY="image.processing.requested.v1"; STARTED_KEY="image.analysis.started.v1"; COMPLETED_KEY="image.analysis.completed.v1"; FAILED_KEY="image.analysis.failed.v1"; QUEUE="aerocompliance.ai.image-processing.v1"; DLQ="aerocompliance.ai.image-processing.dlq.v1"
ready=False
app=Flask(__name__)
@app.get("/health")
def health(): return jsonify(status="alive")
@app.get("/ready")
def readiness(): return (jsonify(status="ready"),200) if ready else (jsonify(status="starting"),503)
def utcnow(): return datetime.now(timezone.utc).isoformat().replace("+00:00","Z")

class Worker:
    def __init__(self, settings):
        self.s=settings; self.minio=Minio(settings.minio_endpoint,access_key=settings.minio_access_key,secret_key=settings.minio_secret_key,secure=settings.minio_secure)
        self.runner=MockModelRunner() if settings.mode=="mock" else YoloModelRunner(settings)
    def publish(self,ch,key,payload):
        eid=payload["eventId"]; published=ch.basic_publish(EXCHANGE,key,json.dumps(payload,separators=(",",":")),pika.BasicProperties(content_type="application/json",delivery_mode=2,message_id=eid,type=key),mandatory=False)
        if published is False: raise RuntimeError("RabbitMQ did not confirm publication")
    def callback(self,ch,method,properties,body):
        request=None; started_at=utcnow(); started_clock=time.monotonic()
        try:
            request=json.loads(body); required=("eventId","requestId","imageId","bucket","objectKey","sha256")
            if any(not request.get(k) for k in required): raise ModelFailure("MALFORMED_REQUEST","Required request fields are missing.")
            base={"schemaVersion":1,"correlationId":request["correlationId"],"causationId":request["eventId"],"occurredAt":started_at,"requestId":request["requestId"],"imageId":request["imageId"],"attempt":1}
            self.publish(ch,STARTED_KEY,{**base,"eventId":str(uuid.uuid5(uuid.UUID(request["requestId"]),"started")),"mode":self.s.mode,"modelName":self.s.model_name,"modelVersion":self.s.model_version,"confidenceThreshold":self.s.threshold,"startedAt":started_at})
            with tempfile.NamedTemporaryFile(delete=False) as temp: path=temp.name
            try:
                self.minio.fget_object(request["bucket"],request["objectKey"],path)
                digest=hashlib.sha256()
                with open(path,"rb") as source:
                    for chunk in iter(lambda:source.read(1024*1024),b""): digest.update(chunk)
                if digest.hexdigest().lower()!=request["sha256"].lower(): raise ModelFailure("HASH_MISMATCH","Stored object hash does not match the processing request.")
                detections=self.runner.analyze(request); completed_at=utcnow()
                self.publish(ch,COMPLETED_KEY,{**base,"eventId":str(uuid.uuid5(uuid.UUID(request["requestId"]),"completed")),"occurredAt":completed_at,"mode":self.s.mode,"modelName":self.s.model_name,"modelVersion":self.s.model_version,"confidenceThreshold":self.s.threshold,"sourceCrs":None,"outputCrs":"EPSG:4326","startedAt":started_at,"completedAt":completed_at,"durationMs":int((time.monotonic()-started_clock)*1000),"detections":detections})
            finally:
                if os.path.exists(path): os.remove(path)
            ch.basic_ack(method.delivery_tag)
        except Exception as exc:
            code=exc.code if isinstance(exc,ModelFailure) else "AI_WORKER_ERROR"; message=str(exc)[:500] if isinstance(exc,ModelFailure) else "AI processing failed safely."
            if request and request.get("requestId") and request.get("imageId"):
                failed_at=utcnow(); base={"schemaVersion":1,"correlationId":request.get("correlationId",request["requestId"]),"causationId":request.get("eventId"),"occurredAt":failed_at,"requestId":request["requestId"],"imageId":request["imageId"],"attempt":1}
                self.publish(ch,FAILED_KEY,{**base,"eventId":str(uuid.uuid5(uuid.UUID(request["requestId"]),"failed")),"stage":"analysis","errorCode":code,"message":message,"retryable":code not in ("MALFORMED_REQUEST","MISSING_CAPTURE_COORDINATES"),"failedAt":failed_at})
                ch.basic_ack(method.delivery_tag)
            else: ch.basic_nack(method.delivery_tag,requeue=False)
    def run(self):
        global ready
        while True:
            try:
                conn=pika.BlockingConnection(pika.URLParameters(self.s.rabbitmq_url));ch=conn.channel();ch.confirm_delivery();ch.exchange_declare(EXCHANGE,"topic",durable=True);ch.queue_declare(DLQ,durable=True);ch.queue_bind(DLQ,EXCHANGE,"image.processing.dead.v1");ch.queue_declare(QUEUE,durable=True,arguments={"x-dead-letter-exchange":EXCHANGE,"x-dead-letter-routing-key":"image.processing.dead.v1"});ch.queue_bind(QUEUE,EXCHANGE,REQUEST_KEY);ch.basic_qos(prefetch_count=1)
                if not self.minio.bucket_exists(self.s.minio_bucket): self.minio.make_bucket(self.s.minio_bucket)
                ready=True;ch.basic_consume(QUEUE,self.callback,auto_ack=False);ch.start_consuming()
            except Exception as exc: ready=False;print(f"worker connection failed: {type(exc).__name__}",flush=True);time.sleep(3)

def main():
    settings=Settings();threading.Thread(target=lambda:app.run(host="0.0.0.0",port=settings.health_port),daemon=True).start();Worker(settings).run()
if __name__=="__main__": main()
