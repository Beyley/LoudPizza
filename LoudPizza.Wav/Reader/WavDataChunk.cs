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
	/// Whether or not the data chunk is silent (eg all 127 for 8-bit PCM and all 0 for 16-bit PCM)
	/// <br/>
	/// This only occurs with `slnt` chunks
	/// </summary>
	public bool Silent;
}
