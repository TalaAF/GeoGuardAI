import uuid

class ModelFailure(Exception):
    def __init__(self, code: str, message: str): super().__init__(message); self.code = code

class MockModelRunner:
    def analyze(self, request: dict) -> list[dict]:
        scenario = (request.get("mockScenario") or "none").lower()
        if scenario == "failure": raise ModelFailure("MOCK_FAILURE", "The requested deterministic mock failure occurred.")
        if scenario == "none": return []
        lon, lat = request.get("longitude"), request.get("latitude")
        if lon is None or lat is None: raise ModelFailure("MISSING_CAPTURE_COORDINATES", "Geographic mock scenarios require capture longitude and latitude.")
        delta = 0.00008
        ring = [[lon-delta,lat-delta],[lon+delta,lat-delta],[lon+delta,lat+delta],[lon-delta,lat+delta],[lon-delta,lat-delta]]
        detection_id = str(uuid.uuid5(uuid.UUID(request["requestId"]), "detection:0"))
        return [{"detectionId": detection_id, "classId": 0, "className": "building", "confidence": 0.93,
                 "geometry": {"type": "MultiPolygon", "coordinates": [[[p for p in ring]]]}}]

class YoloModelRunner:
    def __init__(self, settings):
        try: from ultralytics import YOLO
        except ImportError as exc: raise RuntimeError("YOLO mode requires requirements-yolo.txt") from exc
        self.model = YOLO(settings.model_path)

    def analyze(self, request: dict) -> list[dict]:
        raise RuntimeError("YOLO georeferencing is not enabled in this phase; use AI_MODE=mock.")
