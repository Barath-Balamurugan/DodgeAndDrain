using UnityEngine;

// Procedurally generated sound effects, so the game needs no audio files.
// Each clip is synthesised once and cached by GameFeedback.
public static class FeedbackSounds
{
    private const int SampleRate = 44100;
    private const float TwoPi = Mathf.PI * 2f;

    // Short bright tick for a bullet that hits but doesn't kill.
    public static AudioClip CreateEnemyHit()
    {
        float[] samples = NewBuffer(0.045f);
        System.Random random = new System.Random(11);
        float phase = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            float progress = i / (float)samples.Length;
            phase += TwoPi * Mathf.Lerp(2200f, 900f, progress) / SampleRate;
            float tone = Mathf.Sin(phase) * 0.6f + Square(phase) * 0.2f;
            float noise = NextNoise(random) * 0.25f * Mathf.Pow(1f - progress, 8f);
            samples[i] = (tone * Mathf.Pow(1f - progress, 4f) + noise) * 0.55f;
        }

        return Build("FeedbackEnemyHit", samples);
    }

    // Punchy pop with a crunchy tail; pitched down for heavier enemies at play time.
    public static AudioClip CreateEnemyKill()
    {
        float[] samples = NewBuffer(0.2f);
        System.Random random = new System.Random(23);
        float phase = 0f;
        float lowPassed = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            float progress = i / (float)samples.Length;
            phase += TwoPi * Mathf.Lerp(560f, 80f, Mathf.Sqrt(progress)) / SampleRate;
            float body = (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f)) * Mathf.Pow(1f - progress, 2.2f);

            lowPassed += (NextNoise(random) - lowPassed) * 0.3f;
            float crunch = lowPassed * Mathf.Pow(1f - progress, 5f);
            float click = i < 90 ? (1f - i / 90f) * 0.6f : 0f;

            samples[i] = (body * 0.6f + crunch * 0.9f + click * NextNoise(random)) * 0.7f;
        }

        return Build("FeedbackEnemyKill", samples);
    }

    // Rising "bloop" when a drainable is used up and turns into ammo.
    public static AudioClip CreateDrainablePop()
    {
        float[] samples = NewBuffer(0.11f);
        float phase = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            float progress = i / (float)samples.Length;
            phase += TwoPi * Mathf.Lerp(320f, 980f, progress) / SampleRate;
            float envelope = progress < 0.08f ? progress / 0.08f : Mathf.Pow(1f - progress, 1.6f);
            samples[i] = (Mathf.Sin(phase) + 0.2f * Mathf.Sin(phase * 2f)) * envelope * 0.55f;
        }

        return Build("FeedbackDrainablePop", samples);
    }

    // Seamless one-second loop. Every frequency completes whole cycles in one
    // second, so the loop point is inaudible. GameFeedback raises its pitch with heat.
    public static AudioClip CreateDrainHumLoop()
    {
        float[] samples = new float[SampleRate];

        for (int i = 0; i < samples.Length; i++)
        {
            float time = i / (float)SampleRate;
            float tone =
                Mathf.Sin(TwoPi * 55f * time) * 0.2f +
                Mathf.Sin(TwoPi * 110f * time) * 0.5f +
                Mathf.Sin(TwoPi * 220f * time) * 0.25f +
                Mathf.Sin(TwoPi * 330f * time) * 0.12f;
            float tremolo = 0.75f + 0.25f * Mathf.Sin(TwoPi * 6f * time);
            samples[i] = tone * tremolo * 0.45f;
        }

        return Build("FeedbackDrainHum", samples);
    }

    // Three buzzy alternating beeps with a hiss tail.
    public static AudioClip CreateOverheatAlarm()
    {
        float[] samples = NewBuffer(0.6f);
        System.Random random = new System.Random(37);
        float[] beepFrequencies = { 880f, 620f, 880f };
        const float beepLength = 0.13f;
        const float beepSpacing = 0.17f;
        float lowPassed = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            float time = i / (float)SampleRate;
            float progress = i / (float)samples.Length;
            float sample = 0f;

            int beep = Mathf.FloorToInt(time / beepSpacing);
            float beepTime = time - beep * beepSpacing;
            if (beep < beepFrequencies.Length && beepTime < beepLength)
            {
                float beepEnvelope = Mathf.Min(1f, beepTime / 0.005f) * Mathf.Pow(1f - beepTime / beepLength, 0.6f);
                sample += Soft(Mathf.Sin(TwoPi * beepFrequencies[beep] * time) * 3f) * beepEnvelope * 0.5f;
            }

            lowPassed += (NextNoise(random) - lowPassed) * 0.15f;
            sample += lowPassed * (1f - progress) * 0.35f;

            samples[i] = sample * 0.8f;
        }

        return Build("FeedbackOverheatAlarm", samples);
    }

    // Two rising notes: drain is usable again.
    public static AudioClip CreateOverheatRecovered()
    {
        return BuildNotes("FeedbackOverheatRecovered", 0.26f, new[] { 660f, 990f }, 0.09f, 14f, 0.4f);
    }

    // Rising sweep with shimmer for "WAVE N".
    public static AudioClip CreateWaveStart()
    {
        float[] samples = NewBuffer(0.6f);
        float phase = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            float time = i / (float)SampleRate;
            float progress = i / (float)samples.Length;
            phase += TwoPi * Mathf.Lerp(180f, 720f, progress * progress) / SampleRate;

            float envelope = Mathf.Min(1f, progress / 0.08f) * (progress > 0.7f ? 1f - (progress - 0.7f) / 0.3f : 1f);
            float shimmer = Mathf.Sin(phase * 4f) * 0.15f * (0.5f + 0.5f * Mathf.Sin(TwoPi * 12f * time));
            samples[i] = (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f) + shimmer) * envelope * 0.4f;
        }

        return Build("FeedbackWaveStart", samples);
    }

    // Bright major arpeggio for a cleared wave.
    public static AudioClip CreateWaveClear()
    {
        return BuildNotes("FeedbackWaveClear", 0.7f, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.08f, 7f, 0.32f);
    }

    // Bell-like chord for picking a power-up card.
    public static AudioClip CreatePowerUpSelected()
    {
        float[] samples = NewBuffer(0.35f);

        for (int i = 0; i < samples.Length; i++)
        {
            float time = i / (float)SampleRate;
            float envelope = Mathf.Min(1f, time / 0.004f) * Mathf.Exp(-time * 10f);
            float chord = Mathf.Sin(TwoPi * 880f * time) + 0.7f * Mathf.Sin(TwoPi * 1320f * time) + 0.3f * Mathf.Sin(TwoPi * 1760f * time);
            samples[i] = chord * envelope * 0.3f;
        }

        return Build("FeedbackPowerUpSelected", samples);
    }

    // Two soft blips when ammo drops into the warning zone.
    public static AudioClip CreateLowAmmo()
    {
        float[] samples = NewBuffer(0.16f);

        for (int i = 0; i < samples.Length; i++)
        {
            float time = i / (float)SampleRate;
            float blipTime = time < 0.08f ? time : time - 0.08f;
            if (blipTime > 0.05f)
                continue;

            float envelope = Mathf.Sin(blipTime / 0.05f * Mathf.PI);
            samples[i] = Triangle(TwoPi * 760f * time) * envelope * 0.4f;
        }

        return Build("FeedbackLowAmmo", samples);
    }

    private static AudioClip BuildNotes(string name, float duration, float[] frequencies, float noteSpacing, float decay, float volume)
    {
        float[] samples = NewBuffer(duration);

        for (int i = 0; i < samples.Length; i++)
        {
            float time = i / (float)SampleRate;
            float sample = 0f;

            for (int note = 0; note < frequencies.Length; note++)
            {
                float noteTime = time - note * noteSpacing;
                if (noteTime < 0f)
                    continue;

                float envelope = Mathf.Min(1f, noteTime / 0.004f) * Mathf.Exp(-noteTime * decay);
                sample += (Mathf.Sin(TwoPi * frequencies[note] * time) + 0.25f * Mathf.Sin(TwoPi * frequencies[note] * 2f * time)) * envelope;
            }

            samples[i] = sample * volume;
        }

        return Build(name, samples);
    }

    private static float[] NewBuffer(float duration)
    {
        return new float[Mathf.CeilToInt(SampleRate * duration)];
    }

    private static AudioClip Build(string name, float[] samples)
    {
        for (int i = 0; i < samples.Length; i++)
            samples[i] = Mathf.Clamp(samples[i], -1f, 1f);

        AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        clip.hideFlags = HideFlags.HideAndDontSave;
        return clip;
    }

    private static float NextNoise(System.Random random) => (float)(random.NextDouble() * 2.0 - 1.0);
    private static float Square(float phase) => Mathf.Sin(phase) >= 0f ? 1f : -1f;
    private static float Triangle(float phase) => Mathf.PingPong(phase * 2f / Mathf.PI, 2f) - 1f;
    private static float Soft(float value) => value / (1f + Mathf.Abs(value));
}
