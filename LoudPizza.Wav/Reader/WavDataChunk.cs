namespace LoudPizza.Wav.Reader; 

public struct WavDataChunk {
	/// <summary>
	/// The offset of the data chunk in the file (in bytes)
	/// </summary>
	public int FileOffset;
	/// <summary>
	/// The offset of the data chunk in the file (in samples)
	/// </summary>
	public int SampleStart;
	/// <summary>
	/// The size of the data chunk (in samples)
	/// </summary>
	public int SampleCount;
	/// <summary>
	/// The size of the data chunk (in bytes)
	/// </summary>
	public int ChunkSize;
	/// <summary>
	/// Whether or not the data chunk is silent
	/// (specified to maintain the same value as the previous data sample, or the value of the next data sample, if there is no previous data sample)
	/// <br/>
	/// This only occurs with `slnt` chunks
	/// </summary>
	public bool Silent;
}
