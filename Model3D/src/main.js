import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { VRMLoaderPlugin, VRMUtils } from '@pixiv/three-vrm';
import { loadMixamoAnimation } from './loadMixamoAnimation.js';
import * as TWEEN from 'three/addons/libs/tween.module.js';
import { LipSync } from './LipSync.js';

// --- Renderer ---
const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: "high-performance" });
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
  'Angry', 
  'Bashful', 
  'HeadNodYes', 
  'LookAround', 
  'SarcasticHeadNod', 
  'ShakingHeadNo', 
  'StandingIdle', 
  'TalkingAskQuestion', 
  'Thankful', 
  'Thinking', 
  'Threatening', 
  'WeightShift', 
  'Yawn'
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

  if (newAction === oldAction && newAction.isRunning()) return;

  // Настройка новой анимации
  newAction.enabled = true;
  newAction.setEffectiveTimeScale(1);
  newAction.setEffectiveWeight(1); 
  newAction.reset();
  newAction.setLoop(isLooped ? THREE.LoopRepeat : THREE.LoopOnce);
  newAction.clampWhenFinished = true;

  if (oldAction) {
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
          const idleUrl = '/animations/StandingIdle.fbx';
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
  blinkTimer += deltaTime;
  if (blinkTimer >= nextBlinkTime) {
    blinkTimer = 0;
    nextBlinkTime = Math.random() * 4 + 2;
    new TWEEN.Tween({ val: 0 }).to({ val: 1 }, 100).onUpdate((o) => currentVrm.expressionManager.setValue('blink', o.val))
      .onComplete(() => {
        new TWEEN.Tween({ val: 1 }).to({ val: 0 }, 100).onUpdate((o) => currentVrm.expressionManager.setValue('blink', o.val)).start();
      }).start();
  }
}

// --- Render Loop ---
function animate() {
  requestAnimationFrame(animate);
  const deltaTime = clock.getDelta();
  const { volume } = lipSync.update();
  TWEEN.update();

  if (currentMixer) {
    currentMixer.update(deltaTime);
  }

  if (currentVrm) {
    currentVrm.update(deltaTime);

    // Логика плавного перехода (Anti-Shock)
    const isStationary = currentAction && !currentAction.isRunning();

    // Плавно меняем вес процедурной анимации (0.0 <-> 1.0)
    // 1.0 * deltaTime означает, что полный переход займет 1 секунду
    if (isStationary) {
      proceduralWeight = THREE.MathUtils.lerp(proceduralWeight, 1, 2.0 * deltaTime);
    } else {
      proceduralWeight = THREE.MathUtils.lerp(proceduralWeight, 0, 10.0 * deltaTime);
    }

    if (proceduralWeight > 0.001) {
      const time = Date.now() * 0.001;

      // Вычисляем базовые значения
      const swayZ = Math.sin(time * 0.5) * 0.012;
      const swayX = Math.cos(time * 0.4) * 0.012;

      // Применяем их, умножая на proceduralWeight для мягкого входа
      currentVrm.scene.rotation.z = swayZ * proceduralWeight;
      currentVrm.scene.rotation.x = swayX * proceduralWeight;
      currentVrm.scene.position.y = (-Math.abs(swayZ) * 0.2) * proceduralWeight;

      // Балансировка Spine
      const spine = currentVrm.humanoid.getRawBoneNode('spine');
      if (spine) {
        spine.rotation.z += (-swayZ * 1.2) * proceduralWeight;
        spine.rotation.x += (-swayX * 0.8) * proceduralWeight;
      }

      // Дыхание грудью
      const chest = currentVrm.humanoid.getRawBoneNode('upperChest') || currentVrm.humanoid.getRawBoneNode('chest');
      if (chest) {
        const breathing = (Math.sin(time * 1.4) * 0.03) * proceduralWeight;
        chest.rotation.x += breathing;
      }

      // Голова
      const head = currentVrm.humanoid.getRawBoneNode('head');
      if (head) {
        head.rotation.z += (-swayZ * 0.6) * proceduralWeight;
        head.rotation.x += (-swayX * 0.6) * proceduralWeight;
        head.rotation.y += (Math.sin(time * 0.6) * 0.04) * proceduralWeight;
      }
    }

    // Если мы выходим из состояния покоя, плавно сбрасываем ротацию сцены в 0
    if (!isStationary) {
      currentVrm.scene.rotation.z = THREE.MathUtils.lerp(currentVrm.scene.rotation.z, 0, 0.1);
      currentVrm.scene.rotation.x = THREE.MathUtils.lerp(currentVrm.scene.rotation.x, 0, 0.1);
      currentVrm.scene.position.y = THREE.MathUtils.lerp(currentVrm.scene.position.y, 0, 0.1);
    }

    updateBlink(deltaTime);

    // LipSync (без изменений)
    const minThreshold = 0.20;
    let shapedVolume = volume > minThreshold ? (volume - minThreshold) * 3.0 : 0;
    shapedVolume = Math.min(shapedVolume, 1.0);
    smoothedVolume += (shapedVolume - smoothedVolume) * 0.2;
    currentVrm.expressionManager.setValue('aa', smoothedVolume);
    currentVrm.expressionManager.update();
  }

  renderer.render(scene, camera);
}
animate();

// --- Speech & Emotions ---
window.say = async (data, pitch, port, service, model, language, speaker, volume, bass, treble) => {
  const response = await fetch(`http://127.0.0.1:${port}/${service}?text=${encodeURIComponent(data.cleanText)}&model_path=${encodeURIComponent(model)}&language=${language}&speaker=${speaker}`);
  const audioBuffer = await audioContext.decodeAudioData(await response.arrayBuffer());
  const sourceNode = audioContext.createBufferSource();
  sourceNode.buffer = audioBuffer;
  sourceNode.playbackRate.value = pitch;

  const gainNode = audioContext.createGain();
  gainNode.gain.value = volume;
  sourceNode.connect(lipSync.analyser);
  sourceNode.connect(gainNode);
  gainNode.connect(audioContext.destination);
  sourceNode.start();

  const realCharSpeed = (audioBuffer.duration / data.cleanText.length) * 1000;
  setEmotions(data, realCharSpeed);
};

async function setEmotions(data, charSpeed) {
  if (!data.emotions) return;

  data.emotions.forEach((item) => {
    const delay = item.pos * charSpeed;

    setTimeout(() => {
      const vrm = currentVrm;
      if (!vrm) return;

      console.log(`[Event] Сработал ${item.name} через ${delay}ms`);

      if (item.name.startsWith('motion:')) {
        const animName = item.name.replace('motion:', '');
        loadFBX(`/animations/${animName}.fbx`, false);
      }
      else if (item.name.startsWith('mood:')) {
        const emotionName = item.name.replace('mood:', '');

        // Плавный сброс всех КРОМЕ текущей
        vrm.expressionManager.expressions.forEach((ex) => {
          if (ex.expressionName !== emotionName && !['aa', 'ee', 'ih', 'oh', 'ou', 'blink'].includes(ex.expressionName)) {
            const val = vrm.expressionManager.getValue(ex.expressionName);
            if (val > 0) {
              new TWEEN.Tween({ v: val }).to({ v: 0 }, 500)
                .onUpdate(o => vrm.expressionManager.setValue(ex.expressionName, o.v)).start();
            }
          }
        });

        // Включение новой
        new TWEEN.Tween({ w: vrm.expressionManager.getValue(emotionName) || 0 })
          .to({ w: 1.0 }, 400)
          .onUpdate(o => {
            vrm.expressionManager.setValue(emotionName, o.w)
            if (emotionName === 'surprised') {
              vrm.expressionManager.setValue('ih', 0);
              vrm.expressionManager.setValue('oh', 0);
              vrm.expressionManager.setValue('aa', 0);
            }
          })
          .start();
      }
    }, delay);
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