import os
import re
import time
import torch
import uvicorn
from fastapi import FastAPI, Query
from fastapi.responses import Response, StreamingResponse
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import List

app = FastAPI()

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

loaded_models = {} 

def get_model(model_path, language):
    if model_path in loaded_models:
        return loaded_models[model_path]
    if not os.path.exists(model_path):
        print(f"ERROR: Файл не найден: {model_path}")
        return None
    print(f"DEBUG: Попытка загрузки через torch.package: {model_path}")
    for _ in range(5):
        try:
            importer = torch.package.PackageImporter(model_path)
            model = importer.load_pickle("tts_models", "model")
            loaded_models[model_path] = model
            model.to(torch.device('cpu'))
            return model
        except RuntimeError as e:
            if "Permission denied" in str(e):
                time.sleep(0.3)
                continue
            raise e

def split_text_into_chunks(text: str) -> List[str]:
    """Разбивает текст на небольшие фразы по знакам препинания."""
    raw_chunks = re.split(r'([.!?,\n]+)', text)
    chunks = []
    current_chunk = ""

    for part in raw_chunks:
        current_chunk += part
        if re.search(r'[.!?,\n]', part) and len(current_chunk.strip()) > 0:
            chunks.append(current_chunk.strip())
            current_chunk = ""

    if current_chunk.strip():
        chunks.append(current_chunk.strip())

    return chunks if chunks else [text]

class EmotionItem(BaseModel):
    name: str
    pos: int

class TimingRequest(BaseModel):
    cleanText: str
    emotions: List[EmotionItem]

@app.post("/generate_timings")
async def generate_timings(data: TimingRequest):
    timings = []
    for em in data.emotions:
        timings.append({"expression": em.name, "time_ms": em.pos})
    return timings

@app.get("/speakers")
async def get_speakers(model_path: str, language: str):
    model = get_model(model_path, language)
    if not model:
        return {"error": "Model not found"}
    return {"speakers": model.speakers}

# 1. ОБЫЧНЫЙ РЕЖИМ (Генерация целиком в WAV) - для isStream = false
@app.get("/silero_tts")
async def silero_tts(
    model_path: str, 
    language: str, 
    text: str = Query(...), 
    speaker: str = "baya", 
    sample_rate: int = 48000
):
    model = get_model(model_path, language)
    if not model:
        return Response(status_code=404, content="Model not found")
    
    audio_path = model.save_wav(text=text, speaker=speaker, sample_rate=sample_rate)
    with open(audio_path, "rb") as f:
        audio_data = f.read()
    return Response(content=audio_data, media_type="audio/wav")

# 2. ПОТОКОВЫЙ РЕЖИМ (Streaming PCM) - для isStream = true
@app.get("/silero_tts_stream")
async def silero_tts_stream(
    model_path: str, 
    language: str, 
    text: str = Query(...), 
    speaker: str = "baya", 
    sample_rate: int = 48000
):
    model = get_model(model_path, language)
    if not model:
        return Response(status_code=404, content="Model not found")

    chunks = split_text_into_chunks(text)

    def audio_stream_generator():
        for chunk in chunks:
            if not chunk.strip():
                continue
            audio_tensor = model.apply_tts(
                text=chunk, 
                speaker=speaker, 
                sample_rate=sample_rate
            )
            pcm_bytes = (
                (audio_tensor * 32767)
                .clamp(-32768, 32767)
                .to(torch.int16)
                .numpy()
                .tobytes()
            )
            yield pcm_bytes

    return StreamingResponse(audio_stream_generator(), media_type="audio/pcm")

@app.get("/health")
async def health():
    return {"status": "ok"}

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=5050)