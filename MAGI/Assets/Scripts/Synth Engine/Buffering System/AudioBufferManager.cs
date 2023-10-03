﻿using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using General.Data_Containers;
using Synth_Engine.Buffering_System.Buffer_Data;

namespace Synth_Engine.Buffering_System
{
    public static class AudioBufferManager
    {
        #region Variable Declarations

        // Values concerning the audio buffer sizes
        private const int MonoBufferSize = 2048;
        private const int MonoBufferByteSize = MonoBufferSize * sizeof(float);
        private const int StereoBufferSize = MonoBufferSize / 2;

        // Current audio buffer in use
        private static readonly float[] CurrentAudioBuffer = new float[MonoBufferSize];

        // Buffer that will be used next
        private static readonly float[] NextAudioBuffer = new float[MonoBufferSize];

        // Stereo audio buffer storage
        private static readonly StereoData[] StereoAudioBuffer = new StereoData[StereoBufferSize];

        // Preloaded audio buffers with associated frequency and phase data
        private static ConcurrentDictionary<float, (StereoData[], (float, float))> _preloadAudioBuffers;

        #endregion

        #region Preload Buffers
        
                /// <summary>
        ///   Initializes the preload buffer storage based on a given frequency table.
        /// </summary>
        /// <param name="frequencyTable"> the frequency table scriptable object to use </param>
        public static void InitializePreloadBuffers(FrequencyTable frequencyTable)
        {
            _preloadAudioBuffers =
                new ConcurrentDictionary<float, (StereoData[], (float, float))>(1,
                    frequencyTable.Count);
        }

        private static void FillPreloadAudioBuffers(
            FrequencyTable frequencyTable,
            Func<float, float, float, (float, float)> generator,
            float amplitude
        )
        {
            FillPreloadAudioBuffers(
                frequencyTable,
                generator,
                generator,
                amplitude,
                amplitude
            );
        }

        private static void FillPreloadAudioBuffers(
            FrequencyTable frequencyTable,
            Func<float, float, float, (float, float)> leftGenerator,
            Func<float, float, float, (float, float)> rightGenerator,
            float leftAmplitude,
            float rightAmplitude
        )
        {
            // generate the buffers using parallelization
            Parallel.ForEach(frequencyTable, frequency =>
            {
                var stereoBuffer = new StereoData[StereoBufferSize];

                // track this outside of the loop to avoid unnecessary calculations and race conditions
                var phaseLeft = 0f;
                var phaseRight = 0f;

                // generate the buffer
                for (var i = 0; i < StereoBufferSize; i++)
                {
                    // generate the left and right channels using the generators and the current phase
                    var (left, updatedLeftPhase) = leftGenerator(frequency, phaseLeft, leftAmplitude);
                    var (right, updatedRightPhase) = rightGenerator(frequency, phaseRight, rightAmplitude);

                    stereoBuffer[i] = new StereoData(left, right);

                    phaseLeft = updatedLeftPhase;
                    phaseRight = updatedRightPhase;
                }

                // add the buffer to the dictionary with the associated frequency and phase data
                _preloadAudioBuffers.TryAdd(frequency, (stereoBuffer, (phaseLeft, phaseRight)));
            });
        }

        /// <summary>
        ///  Fills the preload audio buffers using a given generator and amplitude.
        ///  Can be used to generate a mono buffer (left and right channels are the same).
        /// </summary>
        /// <param name="frequency"></param>
        public static void SetPreloadAudioBuffer(float frequency)
        {
            _preloadAudioBuffers[frequency].Item1.CopyToFloatArray(CurrentAudioBuffer);
        }

        #endregion

        #region Audio Buffer Generation
        
        /// <summary>
        ///  Fills the next audio buffer using a given generator and amplitude and frequency.
        /// </summary>
        /// <param name="generator"> the generator of the audio </param>
        /// <param name="frequency"> the frequency of the given audio </param>
        /// <param name="amplitude"> the amplitude of the given audio </param>
        public static void FillNextAudioBuffer(
            Func<float, float, float, (float, float)> generator,
            float frequency,
            float amplitude
        )
        {
            FillNextAudioBuffer(
                generator,
                generator,
                frequency,
                amplitude,
                amplitude
            );
        }

        /// <summary>
        /// Fills the next audio buffer for both left and right channels using provided generators and amplitudes.
        /// </summary>
        private static void FillNextAudioBuffer(
            Func<float, float, float, (float, float)> generatorLeft,
            Func<float, float, float, (float, float)> generatorRight,
            float frequency,
            float amplitudeLeft,
            float amplitudeRight
        )
        {
            var bufferData = _preloadAudioBuffers[frequency];

            var phaseLeft = bufferData.Item2.Item1;
            var phaseRight = bufferData.Item2.Item2;

            for (var i = 0; i < StereoBufferSize; i++)
            {
                var (left, updatedPhaseLeft) = generatorLeft(frequency, amplitudeLeft, phaseLeft);
                var (right, updatedPhaseRight) = generatorRight(frequency, amplitudeRight, phaseRight);

                StereoAudioBuffer[i] = new StereoData(left, right);

                phaseLeft = updatedPhaseLeft;
                phaseRight = updatedPhaseRight;
            }

            // update the phase data in the dictionary for the given frequency
            _preloadAudioBuffers[frequency] = (bufferData.Item1, (phaseLeft, phaseRight));
        
            // copy the stereo audio buffer to the next audio buffer
            StereoAudioBuffer.CopyToFloatArray(NextAudioBuffer);
        }

        /// <summary>
        /// Gets the current audio buffer.
        /// </summary>
        /// <param name="bufferOut">Array to copy current audio buffer data to.</param>
        public static void GetAudioBuffer(float[] bufferOut)
        {
            if (bufferOut is not {Length: MonoBufferSize})
            {
                throw new ArgumentException("Invalid buffer provided.", nameof(bufferOut));
            }
            CopyAudioBuffer(CurrentAudioBuffer, bufferOut);
        }

        /// <summary>
        /// Switches the audio buffers, making the next audio buffer the current one.
        /// </summary>
        public static void SwitchAudioBuffers()
        {
            CopyAudioBuffer(NextAudioBuffer, CurrentAudioBuffer);
        }

        /// <summary>
        /// Copies audio data from source to destination buffer.
        /// </summary>
        private static void CopyAudioBuffer(float[] source, float[] destination)
        {
            // block copy is faster than Array.Copy and Buffer.MemoryCopy
            // it copies the data in chunks of 4 bytes (float size) instead of 1 byte
            // useful for copying large amounts of data like my audio buffers
            Buffer.BlockCopy(
                source,
                0,
                destination,
                0,
                MonoBufferByteSize
            );
        }

        #endregion
    }
}