using LoudPizza;
using LoudPizza.Core;

namespace LoudPizza.Test.Shared;

public interface IAudioBackend {
	public SoLoud SoLoud { get; }
	
	/// <summary>
	/// Initialize the audio backend
	/// </summary>
	/// <param name="sampleRate">The sample rate of the device</param>
	/// <param name="bufferSize">Size of the audio buffer, in samples</param>
	/// <param name="channels">The amount of audio channels (1 = mono, 2 = stereo)</param>
	/// <returns></returns>
	public SoLoudStatus Init(uint sampleRate = 48000, uint bufferSize = 2048, uint channels = 2);
}
