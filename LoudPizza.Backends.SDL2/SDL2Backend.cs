using LoudPizza.Core;
using LoudPizza.Test.Shared;

namespace LoudPizza.Backends.SDL2;

// ReSharper disable once InconsistentNaming
public class SDL2Backend : IAudioBackend {
	public SoLoud SoLoud {
		get;
	}
	public SoLoudStatus Init(uint sampleRate = 48000, uint bufferSize = 2048, uint channels = 2) {
		throw new NotImplementedException();
	}
}
