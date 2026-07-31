import io
import os
import re
import time
import wave
import numpy as np
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

def split_text_into_chunks(text: str, max_chars: int = 350) -> List[str]:
    """
    Разбивает текст по знакам препинания и гарантирует, 
    что ни один чанк не превысит max_chars (безопасный лимит Silero).
    """
    raw_chunks = re.split(r'([.!?,\n]+)', text)
    chunks = []
    current_chunk = ""

    for part in raw_chunks:
        if len(current_chunk) + len(part) <= max_chars:
            current_chunk += part
            if re.search(r'[.!?,\n]', part) and len(current_chunk.strip()) > 0:
                chunks.append(current_chunk.strip())
                current_chunk = ""
        else:
            if current_chunk.strip():
                chunks.append(current_chunk.strip())
            
            # Если даже одна фраза длиннее max_chars (нет знаков препинания) — режем жестко
            if len(part) > max_chars:
                for i in range(0, len(part), max_chars):
                    sub = part[i:i + max_chars].strip()
                    if sub:
                        chunks.append(sub)
                current_chunk = ""
            else:
                current_chunk = part

    if current_chunk.strip():
        chunks.append(current_chunk.strip())

    return [c for c in chunks if c]

def pcm_tensors_to_wav_bytes(audio_tensors: List[torch.Tensor], sample_rate: int) -> bytes:
    """Склеивает список тензоров и кодирует их в WAV (PCM 16-bit) в памяти."""
    if not audio_tensors:
        return b""
    
    full_audio = torch.cat(audio_tensors, dim=0)
    pcm_int16 = (full_audio * 32767).clamp(-32768, 32767).to(torch.int16).numpy()
    
    wav_buf = io.BytesIO()
    with wave.open(wav_buf, 'wb') as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2) # 16-bit
        wf.setframerate(sample_rate)
        wf.writeframes(pcm_int16.tobytes())
    
    return wav_buf.getvalue()

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

# 1. ОБЫЧНЫЙ РЕЖИМ (Сборка любого по длине текста в единый WAV)
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
    
    chunks = split_text_into_chunks(text)
    audio_tensors = []

    for chunk in chunks:
        if not chunk.strip():
            continue
        audio_tensor = model.apply_tts(
            text=chunk, 
            speaker=speaker, 
            sample_rate=sample_rate
        )
        audio_tensors.append(audio_tensor)

    wav_bytes = pcm_tensors_to_wav_bytes(audio_tensors, sample_rate)
    return Response(content=wav_bytes, media_type="audio/wav")

# 2. ПОТОКОВЫЙ РЕЖИМ (Стриминг PCM кусочками)
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