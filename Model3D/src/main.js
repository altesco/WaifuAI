import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { VRMLoaderPlugin, VRMUtils } from '@pixiv/three-vrm';
import { loadMixamoAnimation } from './loadMixamoAnimation.js';
import * as TWEEN from 'three/addons/libs/tween.module.js';
import { LipSync } from './LipSync.js';

// --- Renderer ---
const renderer = new THREE.WebGLRenderer({
  antialias: true,
  powerPreference: "high-performance",
  preserveDrawingBuffer: true
});
renderer.setClearColor(0xeef1f5, 1);
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.setPixelRatio(window.devicePixelRatio, 2);
document.body.appendChild(renderer.domElement);

// --- Camera ---
const camera = new THREE.PerspectiveCamera(30.0, window.innerWidth / window.innerHeight, 0.1, 20.0);
camera.position.set(0.0, 1.0, 3.0);
const controls = new OrbitControls(camera, renderer.domElement);
controls.target.set(0.0, 0.7, 0.0);
controls.enableRotate = false;
controls.enableZoom = false;
controls.enablePan = false;
controls.update();

// --- Scene ---
const scene = new THREE.Scene();
const light = new THREE.DirectionalLight(0xffffff, Math.PI);
light.position.set(1.0, 1.0, 1.0).normalize();
scene.add(light);

// --- Audio & LipSync ---
const audioContext = new (window.AudioContext || window.webkitAudioContext)();
const lipSync = new LipSync(audioContext);

// --- State ---
const clock = new THREE.Clock(); 
let smoothedVolume = 0;          
let currentVrm = undefined;
let currentMixer = undefined;
let currentAction = undefined;
let idleAction = undefined;
let proceduralWeight = 0;

let blinkTimer = 0;
let nextBlinkTime = Math.random() * 3 + 2;

// animation cache
const ANIMATION_URLS = [
  'angry', 
  'bashful', 
  'head_nod_yes', 
  'look_around', 
  'sarcastic_head_nod', 
  'shaking_head_no', 
  'standing_idle', 
  'talking_ask_question', 
  'thankful', 
  'thinking', 
  'threatening', 
  'weight_shift', 
  'yawn',
  'evil_plotting',
  'talking',
  'happy',
  'loser',
  'crazy_gesture',
  'standing_greeting',
  'excited'
];
const animationCache = new Map();

window.vrmApp = { loadAnim: loadFBX };

async function preloadAnimations(vrm) {
  console.log("Начало предзагрузки анимаций...");

  // Создаем массив промисов для параллельной загрузки
  const promises = ANIMATION_URLS.map(async (name) => {
    const url = `/animations/${name}.fbx`;
    try {
      const clip = await loadMixamoAnimation(url, vrm);
      animationCache.set(url, clip);
      console.log(`[Кэш] Загружено: ${name}`);
    } catch (e) {
      console.error(`[Ошибка кэша] Не удалось загрузить ${name}:`, e);
    }
  });

  await Promise.all(promises);
  console.log("Все анимации готовы!");
}

// --- Animation Loading ---
async function loadFBX(animationUrl, isLooped = false) {
  if (!currentMixer || !currentVrm) return;

  const clip = animationCache.get(animationUrl) || await loadMixamoAnimation(animationUrl, currentVrm);
  if (!animationCache.has(animationUrl)) animationCache.set(animationUrl, clip);

  const newAction = currentMixer.clipAction(clip);
  const oldAction = currentAction;

  // 1. Фикс бага, когда анимация пытается смениться сама на себя
  if (oldAction === newAction) {
    if (newAction.isRunning()) return; // Уже играет - ничего не делаем

    // Если она закончилась, просто перезапускаем без кроссфейда
    newAction.reset();
    newAction.play();
    return;
  }

  // Настройка новой анимации
  newAction.enabled = true;
  newAction.setEffectiveTimeScale(1);
  newAction.setEffectiveWeight(1);
  newAction.reset();
  newAction.setLoop(isLooped ? THREE.LoopRepeat : THREE.LoopOnce);
  newAction.clampWhenFinished = true;

  if (oldAction) {
    // 2. Фикс Т-позы при смене с уже завершенной анимации
    if (!oldAction.isRunning()) {
      // Воскрешаем старую анимацию, чтобы кроссфейд отработал корректно, 
      // заморозив ее на последнем кадре (timeScale = 0)
      oldAction.paused = false;
      oldAction.setEffectiveTimeScale(0);
      oldAction.enabled = true;
      oldAction.play();
    }

    newAction.play();
    oldAction.crossFadeTo(newAction, 0.4, true);
  } else {
    newAction.play();
  }

  currentAction = newAction;
  proceduralWeight = 0;
}

function loadVRM(modelUrl) {
  // Возвращаем Promise, чтобы await в changeModel работал корректно
  return new Promise((resolve, reject) => {
    const loader = new GLTFLoader();
    loader.register((parser) => new VRMLoaderPlugin(parser, { autoUpdateHumanBones: true }));

    loader.load(
      modelUrl,
      async (gltf) => {
        try {
          const vrm = gltf.userData.vrm;
          VRMUtils.removeUnnecessaryVertices(gltf.scene);
          VRMUtils.combineSkeletons(gltf.scene);
          VRMUtils.combineMorphs(vrm);

          if (currentVrm) {
            scene.remove(currentVrm.scene);
            VRMUtils.deepDispose(currentVrm.scene);
          }

          currentVrm = vrm;
          window.vrm = vrm;

          // Настройка поз
          const leftUpperArm = vrm.humanoid.getRawBoneNode('leftUpperArm');
          const rightUpperArm = vrm.humanoid.getRawBoneNode('rightUpperArm');
          if (leftUpperArm) leftUpperArm.rotation.set(0, 0, 0.8);
          if (rightUpperArm) rightUpperArm.rotation.set(0, 0, -0.8);

          scene.add(vrm.scene);
          VRMUtils.rotateVRM0(vrm);
          currentMixer = new THREE.AnimationMixer(vrm.scene);

          // Ждем предзагрузку анимаций
          await preloadAnimations(vrm);

          // Запуск Idle
          const idleUrl = '/animations/standing_idle.fbx';
          if (animationCache.has(idleUrl)) {
            idleAction = currentMixer.clipAction(animationCache.get(idleUrl));
            idleAction.play();
            currentAction = idleAction;
          }

          vrm.scene.traverse((obj) => { obj.frustumCulled = false; });

          console.log("JS: Внутренняя логика загрузки завершена");
          resolve(); // ТОЛЬКО ТЕПЕРЬ Promise считается выполненным
        } catch (err) {
          reject(err);
        }
      },
      (progress) => {
        // Можно логировать прогресс, если надо
        // console.log('Loading...', (progress.loaded / progress.total * 100) + '%');
      },
      (error) => {
        console.error("Ошибка загрузчика:", error);
        reject(error);
      }
    );
  });
}

// --- Blink Logic ---
function updateBlink(deltaTime) {
  if (!currentVrm) return;
  const happyVal = currentVrm.expressionManager.getValue('happy') ||
    currentVrm.expressionManager.getValue('Happy') ||
    currentVrm.expressionManager.getValue('joy') || 0;
  if (happyVal > 0.5) {
    blinkTimer = 0;
    return;
  }
  blinkTimer += deltaTime;
  if (blinkTimer >= nextBlinkTime) {
    blinkTimer = 0;
    nextBlinkTime = Math.random() * 4 + 2;
    new TWEEN.Tween({ val: 0 })
      .to({ val: 1 }, 100)
      .onUpdate((o) => currentVrm.expressionManager.setValue('blink', o.val))
      .onComplete(() => {
        new TWEEN.Tween({ val: 1 })
          .to({ val: 0 }, 100)
          .onUpdate((o) => currentVrm.expressionManager.setValue('blink', o.val))
          .start();
      })
      .start();
  }
}

// --- Render Loop ---
let vState = { aa: 0, ee: 0, oh: 0 };
let debugTimer = 0;
let smoothedVowels = { aa: 0, ee: 0, oh: 0 };

function animate() {
  requestAnimationFrame(animate);
  const deltaTime = clock.getDelta();
  debugTimer += deltaTime;
  if (debugTimer > 0.1) { // Вывод каждые 100мс
    const sync = lipSync.update();
    if (sync.volume > 0.05) { // Выводим только когда есть хоть какой-то звук
      console.log(
        `%c[TTS Debug]%c Vol: ${sync.volume.toFixed(2)} | ` +
        `AA (Mid): ${sync.vowels.aa.toFixed(2)} | ` +
        `EE (High): ${sync.vowels.ee.toFixed(2)} | ` +
        `OH (Low): ${sync.vowels.oh.toFixed(2)}`,
        "color: #00ff00; font-weight: bold;", "color: default;"
      );
    }
    debugTimer = 0;
  }

  // 1. Получаем данные ОДИН раз за кадр
  const syncData = lipSync.update();
  const volume = syncData.volume;
  const vowels = syncData.vowels;

  TWEEN.update();

  if (currentMixer) {
    currentMixer.update(deltaTime);
  }

  if (currentVrm) {
    currentVrm.update(deltaTime);

    // --- Логика Sway/Breathing (твой код) ---
    const isStationary = currentAction && !currentAction.isRunning();
    if (isStationary) {
      proceduralWeight = THREE.MathUtils.lerp(proceduralWeight, 1, 2.0 * deltaTime);
    } else {
      proceduralWeight = THREE.MathUtils.lerp(proceduralWeight, 0, 10.0 * deltaTime);
    }

    if (proceduralWeight > 0.001) {
      const time = Date.now() * 0.001;
      const swayZ = Math.sin(time * 0.5) * 0.012;
      const swayX = Math.cos(time * 0.4) * 0.012;
      currentVrm.scene.rotation.z = swayZ * proceduralWeight;
      currentVrm.scene.rotation.x = swayX * proceduralWeight;
      currentVrm.scene.position.y = (-Math.abs(swayZ) * 0.2) * proceduralWeight;

      const spine = currentVrm.humanoid.getRawBoneNode('spine');
      if (spine) {
        spine.rotation.z += (-swayZ * 1.2) * proceduralWeight;
        spine.rotation.x += (-swayX * 0.8) * proceduralWeight;
      }

      const head = currentVrm.humanoid.getRawBoneNode('head');
      if (head) {
        head.rotation.z += (-swayZ * 0.6) * proceduralWeight;
        head.rotation.x += (-swayX * 0.6) * proceduralWeight;
      }
    }

    // --- ОБНОВЛЕННЫЙ ЛИПСИНК ---
    const minThreshold = 0.18;
    if (volume > minThreshold && vowels) {
      // 1. Усиливаем разницу между гласными (контрастный душ)
      const boost = 5.0;

      // Вычисляем "чистые" значения, вычитая шум других каналов
      // Если OH и AA высокие одновременно, они гасят друг друга, заставляя рот прикрыться
      let pureAA = Math.max(0, vowels.aa - (vowels.oh + vowels.ee) * 0.5);
      let pureEE = Math.max(0, vowels.ee - (vowels.aa + vowels.oh) * 0.5);
      let pureOH = Math.max(0, vowels.oh - (vowels.aa + vowels.ee) * 0.5);

      // 2. Амплитудная огибающая (Envelope)
      // Делаем зависимость от громкости более крутой (квадрат), чтобы на спадах звука рот закрывался резко
      const volumeSq = Math.pow(volume, 2) * 2.0;

      const targetAA = pureAA * volumeSq;
      const targetEE = pureEE * volumeSq;
      const targetOH = pureOH * volumeSq;

      // 3. Асимметричный сглаживатель
      // Открываем чуть медленнее, закрываем ОЧЕНЬ быстро
      const openSpeed = 10.0;
      const closeSpeed = 40.0;

      const lerpVowel = (current, target, dt) => {
        const speed = (target * boost > current) ? openSpeed : closeSpeed;
        return THREE.MathUtils.lerp(current, target * boost, Math.min(dt * speed, 1.0));
      };

      smoothedVowels.aa = lerpVowel(smoothedVowels.aa, targetAA, deltaTime);
      smoothedVowels.ee = lerpVowel(smoothedVowels.ee, targetEE, deltaTime);
      smoothedVowels.oh = lerpVowel(smoothedVowels.oh, targetOH, deltaTime);

      // 4. Ограничение суммарного открытия (Normalization)
      // Это критично: если сумма весов > 1.0, меш не должен "выворачиваться"
      const totalSum = smoothedVowels.aa + smoothedVowels.ee + smoothedVowels.oh + 0.01;
      const limit = totalSum > 1.0 ? 1.0 / totalSum : 1.0;

      currentVrm.expressionManager.setValue('aa', smoothedVowels.aa * limit);
      currentVrm.expressionManager.setValue('ee', smoothedVowels.ee * limit);
      currentVrm.expressionManager.setValue('oh', smoothedVowels.oh * limit);

    } else {
      // Быстрое захлопывание при тишине
      const fallSpeed = 35.0 * deltaTime;
      smoothedVowels.aa = Math.max(0, smoothedVowels.aa - fallSpeed);
      smoothedVowels.ee = Math.max(0, smoothedVowels.ee - fallSpeed);
      smoothedVowels.oh = Math.max(0, smoothedVowels.oh - fallSpeed);

      currentVrm.expressionManager.setValue('aa', smoothedVowels.aa);
      currentVrm.expressionManager.setValue('ee', smoothedVowels.ee);
      currentVrm.expressionManager.setValue('oh', smoothedVowels.oh);
    }

    updateBlink(deltaTime);
    currentVrm.expressionManager.update();
  }

  renderer.render(scene, camera);
}
animate();

let audioStartTime = 0;
let currentAudioSource = null;

// --- Speech & Emotions ---
window.say = async (data, pitch, port, service, model, language, speaker, volume, bass, treble) => {
  const response = await fetch(`http://127.0.0.1:${port}/${service}?text=${encodeURIComponent(data.cleanText)}&model_path=${encodeURIComponent(model)}&language=${language}&speaker=${speaker}`);
  const audioBuffer = await audioContext.decodeAudioData(await response.arrayBuffer());
  
  if (currentAudioSource) {
    try { currentAudioSource.stop(); } catch (e) { }
  }
  
  const sourceNode = audioContext.createBufferSource();
  sourceNode.buffer = audioBuffer;
  sourceNode.playbackRate.value = pitch;
  currentAudioSource = sourceNode;

  const gainNode = audioContext.createGain();
  gainNode.gain.value = volume;
  sourceNode.connect(lipSync.analyser);
  sourceNode.connect(gainNode);
  gainNode.connect(audioContext.destination);
  audioStartTime = audioContext.currentTime;
  sourceNode.start(audioStartTime);

  setEmotions(data, audioBuffer.duration);
};

async function setEmotions(data, duration) {
  if (!data.emotions || !currentVrm) return;
  const vrm = currentVrm;
  const realCharSpeed = (duration / data.cleanText.length);

  data.emotions.forEach((item) => {
    const emotionTimeOffset = item.pos * realCharSpeed;
    const exactStartTime = audioStartTime + (emotionTimeOffset / currentAudioSource.playbackRate.value);
    const now = audioContext.currentTime;
    const timeToWait = (exactStartTime - now) * 1000;

    setTimeout(() => {
      if (!vrm || currentAudioSource?.playbackRate.value === 0) return;

      // 1. Обработка MOTION
      if (item.name.startsWith('motion:')) {
        const animName = item.name.replace('motion:', '');
        loadFBX(`/animations/${animName}.fbx`, false);
      }

      // 2. Обработка MOOD
      else if (item.name.startsWith('mood:')) {
        const rawName = item.name.replace('mood:', '').toLowerCase();

        // Маппинг подмены
        let emotionName = (rawName === 'happy') ? 'relaxed' : rawName;
        if (emotionName === 'surprised') {
          emotionName = 'Surprised';
        }

        console.log(`[Mood Event] Активация: ${emotionName}`);

        // Сброс старых
        vrm.expressionManager.expressions.forEach((ex) => {
          const name = ex.expressionName.toLowerCase();
          if (name !== emotionName && !['aa', 'ee', 'ih', 'oh', 'ou', 'blink'].includes(name)) {
            const currentVal = vrm.expressionManager.getValue(ex.expressionName);
            if (currentVal > 0) {
              new TWEEN.Tween({ v: currentVal })
                .to({ v: 0 }, 500)
                .onUpdate(o => vrm.expressionManager.setValue(ex.expressionName, o.v))
                .start();
            }
          }
        });

        // Включение новой
        const targetVal = (emotionName === 'Surprised') ? 0.3 : 1.0;
        const currentVal = vrm.expressionManager.getValue(emotionName) || 0;

        new TWEEN.Tween({ w: currentVal })
          .to({ w: targetVal }, 400)
          .onUpdate(o => {
            vrm.expressionManager.setValue(emotionName, o.w);
            // Принудительно заставляем VRM пересчитать меш
            vrm.expressionManager.update();
          })
          .start();
      }
    }, Math.max(0, timeToWait));
  });
}

window.addEventListener('resize', () => {
  renderer.setSize(window.innerWidth, window.innerHeight);
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
});

window.addEventListener('click', () => {
  if (audioContext.state === 'suspended') audioContext.resume();
}, { once: true });

window.vrmApp.isModelLoaded = 0;

window.vrmApp.changeModel = async (modelUrl) => {
  window.vrmApp.isModelLoaded = 0;
  try {
    await loadVRM(modelUrl);
    window.vrmApp.isModelLoaded = 1;
  } catch (e) {
    console.error(e);
    window.vrmApp.isModelLoaded = -1;
  }
};

window.vrmApp.setBackground = (hexString) => {
  renderer.setClearColor(hexString, 1);
  document.body.style.backgroundColor = hexString;
  document.documentElement.style.backgroundColor = hexString;
  const canvas = renderer.domElement;
  if (canvas) {
    canvas.style.backgroundColor = hexString;
  }
};

window.vrmApp.printscreen = "";

window.vrmApp.takePrintscreen = () => {
  try {
    const canvas = document.querySelector('canvas');
    if (!canvas) return "null";

    const tempCanvas = document.createElement('canvas');
    const scale = 0.5;
    tempCanvas.width = canvas.width * scale;
    tempCanvas.height = canvas.height * scale;

    const ctx = tempCanvas.getContext('2d');
    ctx.drawImage(canvas, 0, 0, tempCanvas.width, tempCanvas.height);

    window.vrmApp.printscreen = tempCanvas.toDataURL('image/jpeg', 0.8);
  } catch (e) {
    window.vrmApp.printscreen = e.toString();
  }
};