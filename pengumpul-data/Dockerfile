FROM python:3.12-slim
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
WORKDIR /app
COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt
COPY pengumpul ./pengumpul
COPY simbol.toml ./
# jalan terus; menjalankan `harian` tiap hari pada jam_harian di simbol.toml
CMD ["python", "-m", "pengumpul", "jadwal"]
