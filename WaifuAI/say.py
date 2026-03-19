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

'''
device = torch.device('cpu')
model, utils = torch.hub.load(repo_or_dir='snakers4/silero-models',
                              model='silero_tts',
                              language='ru',
                              speaker='v5_2_ru')
model.to(device)'''
loaded_models = {} 

def get_model(model_name):
    if model_name in loaded_models:
        print(f"DEBUG: Уже есть {model_name}...")
        return loaded_models[model_name]
    '''print(f"Загрузка модели для спикера: {model_name}...")
    model, _ = torch.hub.load(repo_or_dir='snakers4/silero-models',
                              model='silero_tts',
                              language='ru',
                              speaker=model_name)
    loaded_models[model_name] = model
    return model'''
    print(f"DEBUG: Начинаю запрос к torch.hub для {model_name}...", flush=True)
    
    try:
        # Добавь trust_repo=True, в новых версиях torch это обязательно
        model, _ = torch.hub.load(repo_or_dir='snakers4/silero-models',
                                  model='silero_tts',
                                  language='ru',
                                  speaker=model_name,
                                  trust_repo=True) 
        loaded_models[model_name] = model
        return model
    except Exception as e:
        print(f"ERROR: Ошибка при загрузке модели: {e}", flush=True)
        raise e

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

@app.get("/speakers")
async def get_speakers(model_name: str = Query(...)):
    model = get_model(model_name)
    return {"speakers": model.speakers}

@app.get("/silero_tts")
async def silero_tts(model_name: str, text: str = Query(...), speaker: str = "baya", sample_rate: int = 48000):
    model = get_model(model_name)
    audio_path = model.save_wav(text=text, speaker=speaker, sample_rate=sample_rate)
    with open(audio_path, "rb") as f:
        audio_data = f.read()
    return Response(content=audio_data, media_type="audio/wav")

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=5050)