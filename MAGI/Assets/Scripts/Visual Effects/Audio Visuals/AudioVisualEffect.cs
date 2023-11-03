﻿using UnityEngine;

namespace Visual_Effects.Audio_Visuals
{
    public abstract class AudioVisualEffect: MonoBehaviour
    {
        [Header("General Settings")]
        [SerializeField] private FrequencyAnalyser frequencyAnalyser;
        [SerializeField] private BandTypes frequencyBandCount = BandTypes.SixtyFour;
        
        /// <summary>
        ///  The frequency analyser that will be used to get the audio data.
        /// </summary>
        protected FrequencyAnalyser FrequencyAnalyser => frequencyAnalyser;
        
        /// <summary>
        ///  The amount of frequency bands that will be used to get the audio data.
        /// </summary>
        protected BandTypes FrequencyBandCount => frequencyBandCount;
        
        /// <summary>
        ///   Initializes the effect by getting the component(s) it needs and setting up any variables it needs.
        /// </summary>
        public abstract void InitializeEffect();
        
        /// <summary>
        ///  Applies the effect by getting the data it needs and applying it to the component(s) it needs.
        /// </summary>
        public abstract void ApplyEffect();
    }
}