FROM python:3.11-alpine
RUN apk add --no-cache ffmpeg
RUN pip install --no-cache-dir flask
COPY app.py /app.py
EXPOSE 5100
CMD ["python", "/app.py"]
