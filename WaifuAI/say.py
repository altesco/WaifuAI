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

device = torch.device('cpu')
model, utils = torch.hub.load(repo_or_dir='snakers4/silero-models',
                              model='silero_tts',
                              language='ru',
                              speaker='v5_2_ru')
model.to(device)

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
        time_ms = int(em.pos * 35)
        timings.append({"expression": em.name, "time_ms": time_ms})
    return timings

@app.get("/silero_tts")
async def silero_tts(text: str = Query(...), speaker: str = "baya", sample_rate: int = 48000):
    audio_path = model.save_wav(text=text, speaker=speaker, sample_rate=sample_rate)
    with open(audio_path, "rb") as f:
        audio_data = f.read()
    return Response(content=audio_data, media_type="audio/wav")

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=5050)