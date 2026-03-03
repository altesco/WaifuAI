""" 
import edge_tts
from fastapi import FastAPI, Query
from fastapi.responses import StreamingResponse
from fastapi.middleware.cors import CORSMiddleware
import uvicorn

app = FastAPI()

# чтобы TypeScript смог забрать поток
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/tts")
async def tts(text: str = Query(..., description="Текст для озвучки")):
    async def generate():
        communicate = edge_tts.Communicate(text, "ru-RU-SvetlanaNeural")
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                yield chunk["data"]
    return StreamingResponse(generate(), media_type="audio/mpeg")

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=5050, log_level="error") """

import edge_tts
import asyncio
import json
import logging
import uvicorn
from fastapi import FastAPI, Query
from fastapi.responses import StreamingResponse
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import List

# --- Настройка логирования ---
logging.basicConfig(
    filename='python_debug.log',
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)

app = FastAPI()

# --- Разрешаем CORS для WebView ---
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# --- Модели данных для POST запроса таймингов ---
class EmotionItem(BaseModel):
    name: str
    pos: int

class TimingRequest(BaseModel):
    cleanText: str
    emotions: List[EmotionItem]

# --- ЭНДПОИНТ: Генерация таймингов ---
@app.post("/generate_timings")
@app.post("/generate_timings")
async def generate_timings(data: TimingRequest):
    logging.info(f"Запрос таймингов получен. Текст: {data.cleanText}")
    logging.debug(f"Эмоции: {data.emotions}")
    
    timings = []
    
    # Расчет времени
    for em in data.emotions:
        # pos - это индекс символа в очищенном тексте
        time_ms = int(em.pos * 35) 
        timings.append({
            "expression": em.name,
            "time_ms": time_ms
        })
        
    return timings

# --- ЭНДПОИНТ: Генерация речи (TTS) ---
@app.get("/tts")
async def tts(text: str = Query(..., description="Текст для озвучки")):
    logging.info(f"Запрос озвучки: {text}")
    
    async def generate():
        communicate = edge_tts.Communicate(text, "ru-RU-SvetlanaNeural")
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                yield chunk["data"]
                
    return StreamingResponse(generate(), media_type="audio/mpeg")

if __name__ == "__main__":
    # Запуск сервера
    uvicorn.run(app, host="127.0.0.1", port=5050, log_level="error")