using LoudPizza.Sources;
using LoudPizza.Wav.Reader;

namespace LoudPizza.Wav;

public class WavAudioStream : IAudioStream {
	private readonly WavReader _wavReader;

	public WavAudioStream(Stream stream, bool keepOpen) {
		this._wavReader = new WavReader(stream, keepOpen);
	}

	public uint  Channels              => (uint)this._wavReader.Channels;
	public float SampleRate            => this._wavReader.SampleRate;
	public float RelativePlaybackSpeed => 1.0f;

	public uint GetAudio(Span<float> buffer, uint samplesToRead, uint channelStride) {
		return this._wavReader.ReadSamples(buffer, samplesToRead, channelStride);
	}
	
	public bool HasEnded() {
		return this._wavReader.EndOfFile;
	}
	
	public bool CanSeek() {
		return true;
	}
	
	public SoLoudStatus Seek(ulong samplePosition, Span<float> scratch, AudioSeekFlags flags, out ulong resultPosition) {
		this._wavReader.SamplePosition = (int)samplePosition;
		resultPosition = (ulong)this._wavReader.SamplePosition;

		return SoLoudStatus.Ok;
	}
	
	public void Dispose() {
		this._wavReader.Dispose();
	}
}
