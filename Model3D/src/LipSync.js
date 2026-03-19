const FREQUENCY_BIN_COUNT = 128; // Этого достаточно для липсинка

export class LipSync {
    constructor(audioContext) {
        this.audio = audioContext;
        this.analyser = this.audio.createAnalyser();

        // Настройка анализатора для частот
        this.analyser.fftSize = 256;
        this.analyser.smoothingTimeConstant = 0.5; // Плавность спектра
        this.frequencyData = new Uint8Array(this.analyser.frequencyBinCount);
    }

    update() {
        this.analyser.getByteFrequencyData(this.frequencyData);

        // 1. Увеличим диапазон, чтобы захватить больше "энергии" голоса
        // Частоты речи распределены до ~3000 Гц.
        const startBin = 3;  // Чуть ниже для басовитых голосов
        const endBin = 40;   // Чуть выше для четкости речи

        let sum = 0;
        for (let i = startBin; i < endBin; i++) {
            sum += this.frequencyData[i];
        }

        // 2. Усредняем. Деление на 255 (макс. значение Byte)
        let volume = sum / ((endBin - startBin) * 255);

        // 3. Возвращаем как есть. 
        // Вся "магия" с порогом и усилением теперь живет в main.js
        return { volume };
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