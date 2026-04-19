const FREQUENCY_BIN_COUNT = 128; // Этого достаточно для липсинка

export class LipSync {
    // constructor(audioContext) {
    //     this.audio = audioContext;
    //     this.analyser = this.audio.createAnalyser();

    //     // Настройка анализатора для частот
    //     this.analyser.fftSize = 256;
    //     this.analyser.smoothingTimeConstant = 0.5; // Плавность спектра
    //     this.frequencyData = new Uint8Array(this.analyser.frequencyBinCount);
    // }

    // update() {
    //     this.analyser.getByteFrequencyData(this.frequencyData);

    //     // 1. Увеличим диапазон, чтобы захватить больше "энергии" голоса
    //     // Частоты речи распределены до ~3000 Гц.
    //     const startBin = 3;  // Чуть ниже для басовитых голосов
    //     const endBin = 40;   // Чуть выше для четкости речи

    //     let sum = 0;
    //     for (let i = startBin; i < endBin; i++) {
    //         sum += this.frequencyData[i];
    //     }

    //     // 2. Усредняем. Деление на 255 (макс. значение Byte)
    //     let volume = sum / ((endBin - startBin) * 255);

    //     // 3. Возвращаем как есть.
    //     // Вся "магия" с порогом и усилением теперь живет в main.js
    //     return { volume };
    // }

    constructor(audioContext) {
        this.audio = audioContext;
        this.analyser = this.audio.createAnalyser();
        this.analyser.fftSize = 512; // 256 может быть мало для точных гласных, 512 — золотая середина
        this.analyser.smoothingTimeConstant = 0.4;

        this.frequencyData = new Uint8Array(this.analyser.frequencyBinCount);

        // Кэшируем индексы бакетов (bins), чтобы не считать их каждый кадр
        const sampleRate = this.audio.sampleRate || 44100;
        const binHz = sampleRate / this.analyser.fftSize;

        this.vowelBins = {
            // О / У — самый низ, убираем всё, что выше 500Гц
            oh: { start: Math.floor(150 / binHz), end: Math.floor(500 / binHz) },
            // А — четкая середина
            aa: { start: Math.floor(700 / binHz), end: Math.floor(1200 / binHz) },
            // И / Е — только высокие частоты, игнорируем середину
            ee: { start: Math.floor(2000 / binHz), end: Math.floor(4500 / binHz) }
        };
    }

    update() {
        this.analyser.getByteFrequencyData(this.frequencyData);

        const calculateEnergy = (range) => {
            let sum = 0;
            for (let i = range.start; i < range.end; i++) sum += this.frequencyData[i];
            let energy = sum / ((range.end - range.start || 1) * 255);
            return Math.pow(energy, 3);
        };

        const vowels = {
            aa: calculateEnergy(this.vowelBins.aa),
            ee: calculateEnergy(this.vowelBins.ee),
            oh: calculateEnergy(this.vowelBins.oh)
        };

        // Считаем общую громкость по узкому диапазону (речь)
        let total = 0;
        for (let i = 8; i < 40; i++) total += this.frequencyData[i];
        const volume = total / (32 * 255);

        return { volume, vowels };
    }

    async playFromArrayBuffer(buffer, onEnded) {
        const audioBuffer = await this.audio.decodeAudioData(buffer);
        const bufferSource = this.audio.createBufferSource();

        bufferSource.buffer = audioBuffer;
        // Подключаем к выходу (колонки) и к нашему анализатору
        bufferSource.connect(this.audio.destination);
        bufferSource.connect(this.analyser);

        bufferSource.start();

        if (onEnded) {
            bufferSource.addEventListener("ended", onEnded);
        }
    }

    async playFromURL(url, onEnded) {
        const res = await fetch(url);
        const buffer = await res.arrayBuffer();
        await this.playFromArrayBuffer(buffer, onEnded);
    }
}