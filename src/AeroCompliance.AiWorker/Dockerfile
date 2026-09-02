FROM python:3.12-slim
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY aerocompliance_ai ./aerocompliance_ai
ENV PYTHONUNBUFFERED=1 AI_MODE=mock HEALTH_PORT=8080
EXPOSE 8080
CMD ["python", "-m", "aerocompliance_ai.main"]
