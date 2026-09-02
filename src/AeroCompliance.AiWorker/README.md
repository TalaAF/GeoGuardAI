# AeroCompliance AI Worker

Runs in `AI_MODE=mock` by default and consumes RabbitMQ processing requests. The default image has
no ML framework or model dependency. Build a separate image with `requirements-yolo.txt` only when
real inference is introduced. Health endpoints are `/health` and `/ready` on port 8080.
