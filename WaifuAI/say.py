import os
import torch
import uvicorn
from fastapi import FastAPI, Query
from fastapi.responses import Response
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
        print(f"{model_path} уже есть")
        return loaded_models[model_path]
    if not os.path.exists(model_path):
        print(f"ERROR: Файл не найден: {model_path}")
        return None
    print(f"DEBUG: Попытка загрузки через torch.package: {model_path}")
    importer = torch.package.PackageImporter(model_path)
    model = importer.load_pickle("tts_models", "model")
    loaded_models[model_path] = model
    model.to(torch.device('cpu'))
    return model

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
    return {"speakers": model.speakers}

@app.get("/silero_tts")
async def silero_tts(model_path: str, language: str, text: str = Query(...), speaker: str = "baya", sample_rate: int = 48000):
    model = get_model(model_path, language)
    audio_path = model.save_wav(text=text, speaker=speaker, sample_rate=sample_rate)
    with open(audio_path, "rb") as f:
        audio_data = f.read()
    return Response(content=audio_data, media_type="audio/wav")

@app.get("/health")
async def health():
    return {"status": "ok"}

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=5050)