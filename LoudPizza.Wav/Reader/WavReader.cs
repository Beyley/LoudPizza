using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using SoftCircuits.Collections;

namespace LoudPizza.Wav.Reader;

public unsafe class WavReader : IDisposable {
	private readonly Stream       _stream;
	private readonly bool         _leaveOpen;
	private readonly long         _startPosition;
	public           int          SamplePosition = 0;
	private readonly BinaryReader _reader;

	private readonly OrderedDictionary<int, WavDataChunk> _dataChunks = new OrderedDictionary<int, WavDataChunk>();

	public WavReader(Stream stream, bool leaveOpen) {
		this._stream    = stream;
		this._reader    = new BinaryReader(stream, Encoding.UTF8, true);
		this._leaveOpen = leaveOpen;

		this._startPosition = stream.Position;

		this.ParseFullFile();
	}

	private static void ThrowInvalidFile() {
		throw new InvalidDataException($"{nameof (_stream)} is not a valid WAV file!");
	}

	private static void EnsureRead(int read, int expected) {
		if (read < expected)
			ThrowInvalidFile();
	}

	public WavFormat Format {
		get;
		private set;
	}

	public int Channels {
		get;
		private set;
	}

	public int SampleRate {
		get;
		private set;
	}

	public int BitsPerSample {
		get;
		private set;
	}

	public bool EndOfFile {
		get;
		private set;
	}

	public uint ReadSamples(Span<float> buffer, uint samplesToRead, uint channelStride) {
		uint read = 0;
		while (samplesToRead > 0) {
			WavDataChunk? chunk = null;

			//Get the data chunk for our current sample position
			foreach ((int key, WavDataChunk value) in this._dataChunks) {
				if (key > this.SamplePosition)
					break;

				chunk = value;
			}

			if (chunk == null)
				throw new Exception($"Unable to find a chunk for sample position {this.SamplePosition}?");

			if (chunk.Value.Silent)
				throw new NotImplementedException("Silent chunks are not implemented yet!");

			//Get the sample offset into the chunk
			int sampleOffset = this.SamplePosition - chunk.Value.SampleStart;

			//Get the byte offset into the chunk for our sample offset
			int byteOffset = sampleOffset * this.Channels * (this.BitsPerSample / 8);

			//Seek to the byte offset in the chunk
			this._stream.Seek(chunk.Value.FileOffset + byteOffset, SeekOrigin.Begin);

			//Get how many WAVE samples to read
			long wavSamplesToRead = samplesToRead * this.Channels;

			//Get how many bytes to read
			long bytesToRead = wavSamplesToRead * (this.BitsPerSample / 8);

			//Get how many bytes are left in the chunk
			long bytesAvailable = chunk.Value.ChunkSize - byteOffset;

			//Get how many bytes we can read from the chunk
			long bytesToReadFromChunk = Math.Min(bytesToRead, bytesAvailable);

			//If we have no bytes to read, we have reached the end of the file
			if (bytesToRead > 0 && bytesAvailable == 0) {
				this.EndOfFile = true;
				return 0;
			}
			this.EndOfFile = false;

			long samplesRead = bytesToReadFromChunk / this.Channels / (this.BitsPerSample / 8);

			long chunkSamplesToRead = samplesRead * this.Channels;

			//Mark that we have read some samples
			samplesToRead       -= (uint)samplesRead;
			read                += (uint)samplesRead;
			this.SamplePosition += (int)samplesRead;

			//Convert the data into the correct un-interlaced float format
			switch (this.Format) {
				case WavFormat.Pcm:
					switch (this.BitsPerSample) {
						case 8: {
							int channel = 0;
							int i       = 0;
							while (chunkSamplesToRead > 0) {
								byte rawSample = this._reader.ReadByte();

								buffer[(int)(i + channel * channelStride)] = rawSample / 128.0f - 1f;

								channel++;
								channel %= this.Channels;

								if (channel == 0) {
									i++;
								}

								chunkSamplesToRead--;
							}
							break;
						}
						case 16: {
							int channel = 0;
							int i       = 0;
							while (chunkSamplesToRead > 0) {
								short rawSample = this._reader.ReadInt16();

								buffer[(int)(i + channel * channelStride)] = rawSample / (float)short.MaxValue;

								channel++;
								channel %= this.Channels;

								if (channel == 0) {
									i++;
								}

								chunkSamplesToRead--;
							}

							break;
						}
						case 24: {
							int channel = 0;
							int i       = 0;
							while (chunkSamplesToRead > 0) {
								byte b0 = this._reader.ReadByte();
								byte b1 = this._reader.ReadByte();
								byte b2 = this._reader.ReadByte();

								int sample = b0 | (b1 << 8) | (b2 << 16);

								sample <<= 8;

								buffer[(int)(i + channel * channelStride)] = sample / (float)0x7fffffff;

								channel++;
								channel %= this.Channels;

								if (channel == 0) {
									i++;
								}

								chunkSamplesToRead--;
							}

							break;
						}
						case 32: {
							int channel = 0;
							int i       = 0;
							while (chunkSamplesToRead > 0) {
								int rawSample = this._reader.ReadInt32();

								buffer[(int)(i + channel * channelStride)] = rawSample / (float)int.MaxValue;

								channel++;
								channel %= this.Channels;

								if (channel == 0) {
									i++;
								}

								chunkSamplesToRead--;
							}

							break;
						}
						default:
							throw new InvalidDataException("Unable to parse WAV PCM data with bits per sample of " + this.BitsPerSample);
					}

					break;
				case WavFormat.Float:
					switch (this.BitsPerSample) {
						case 32: {
							int channel = 0;
							int i       = 0;
							while (chunkSamplesToRead > 0) {
								float rawSample = this._reader.ReadSingle();

								buffer[(int)(i + channel * channelStride)] = rawSample;

								channel++;
								channel %= this.Channels;

								if (channel == 0) {
									i++;
								}

								chunkSamplesToRead--;
							}

							break;
						}
						case 64: {
							int channel = 0;
							int i       = 0;
							while (chunkSamplesToRead > 0) {
								double rawSample = this._reader.ReadDouble();

								buffer[(int)(i + channel * channelStride)] = (float)rawSample;

								channel++;
								channel %= this.Channels;

								if (channel == 0) {
									i++;
								}

								chunkSamplesToRead--;
							}

							break;
						}
						default:
							throw new InvalidDataException("Unable to parse WAV float data with bits per sample of " + this.BitsPerSample);
					}

					break;
			}
		}
		return read;
	}

	/// <summary>
	/// Parses the header of a WAV file, and validates it
	/// </summary>
	/// <returns>The size of the wave chunk</returns>
	private int ParseHeader() {
		//Array to store our magics and identifiers
		byte[] identifierBytes = new byte[4];

		//temp to store the read bytes
		int read = 0;

		//Read the `RIFF` magic
		EnsureRead(read = this._stream.Read(identifierBytes), 4);

		//Check the `RIFF` magic
		if (identifierBytes[0] != 'R' || identifierBytes[1] != 'I' || identifierBytes[2] != 'F' || identifierBytes[3] != 'F')
			ThrowInvalidFile();

		//Get the file size of the `WAVE` chunk
		int waveChunkSize = this._reader.ReadInt32();

		//Read the `WAVE` magic 
		EnsureRead(read = this._stream.Read(identifierBytes), 4);

		//Check the `WAVE` magic
		if (identifierBytes[0] != 'W' || identifierBytes[1] != 'A' || identifierBytes[2] != 'V' || identifierBytes[3] != 'E')
			ThrowInvalidFile();

		return waveChunkSize;
	}

	private void SkipExtraData(int amount) {
		//If we can seek
		if (this._stream.CanSeek)
			//Seek the stream past the chunk to the next chunk
			this._stream.Seek(amount, SeekOrigin.Current);
		else
			//Otherwise, read the chunk into a buffer we are about to throw away
			EnsureRead(this._stream.Read(new byte[amount]), amount);
	}
	
	private void ReadChunk(ref int sampleCount) {
		byte[] identifierBytes = new byte[4];

		//Read the identifier
		if (this._stream.Read(identifierBytes) < 4)
			ThrowInvalidFile();

		//Get the identifier as a string
		string identifier = Encoding.UTF8.GetString(identifierBytes);

		int chunkByteSize = this._reader.ReadInt32();

		//Make sure chunkByteSize is word aligned
		if (chunkByteSize % 2 != 0)
			chunkByteSize++;

		if (identifier == "LIST") {
			long posStart = this._stream.Position;
			long posEnd   = posStart + chunkByteSize;
			
			//Read the list identifier
			if (this._stream.Read(identifierBytes) < 4)
				ThrowInvalidFile();	
			
			//Get the identifier as a string
			identifier = Encoding.UTF8.GetString(identifierBytes);

			if (identifier != "wavl") {
				//Skip the list minus the identifier
				this.SkipExtraData(chunkByteSize - 4);
				return;
			}
			
			//until we are at the end of the list,
			while (this._stream.Position < posEnd) {
				//Read the next chunk
				this.ReadChunk(ref sampleCount);
			}
			return;
		}

		//Specifies the format of the audio data
		if (identifier == "fmt ") {
			//Audio format, 1 for PCM, 3 for IEEE float
			this.Format = (WavFormat)this._reader.ReadInt16();

			if (!Enum.IsDefined(this.Format))
				throw new Exception($"Unknown WAV format! fmt: {(int)this.Format}");

			//The number of channels, ex: 2 for stereo
			this.Channels = this._reader.ReadInt16();

			//Sample rate of the data
			this.SampleRate = this._reader.ReadInt32();

			//The byte rate of the data
			int byteRate = this._reader.ReadInt32();

			//The block align of the data
			short blockAlign = this._reader.ReadInt16();

			//The bits per sample of the data
			this.BitsPerSample = this._reader.ReadInt16();

			//If the length of the format data is greater than 16, then we have extra data, so lets skip past it
			this.SkipExtraData(chunkByteSize - 16);
		}
		//A chunk containing audio data
		else if (identifier == "data") {
			WavDataChunk chunk = new WavDataChunk {
				FileOffset = (int)this._stream.Position,
				ChunkSize  = chunkByteSize,
				//The sample count of the chunk, disregarding the number of channels
				//(since that is what IAudioStream.Seek works with)
				SampleCount = chunkByteSize / (this.BitsPerSample / 8) / this.Channels,
				SampleStart = sampleCount
			};

			//Add the sample count to the total sample count
			sampleCount += chunk.SampleCount;

			//Add the chunk to the dictionary
			this._dataChunks[chunk.SampleStart] = chunk;

			//Skip the chunk data
			this.SkipExtraData(chunkByteSize);
		}
		//indicates a completely silent chunk
		else if (identifier == "slnt") {
			int silentSamples = this._reader.ReadInt32();
			
			WavDataChunk chunk = new WavDataChunk {
				FileOffset = (int)this._stream.Position,
				ChunkSize  = chunkByteSize,
				SampleCount  = silentSamples / this.Channels,
				SampleStart = sampleCount, 
				//This is a silent chunk, mark it as such
				Silent = true
			};
			
			//Add the sample count to the total sample count
			sampleCount += chunk.SampleCount;
		
			//Add the silent chunk to the dictionary
			this._dataChunks[chunk.SampleStart] = chunk;
			
			//Skip any extra data
			this.SkipExtraData(chunkByteSize - 4);
		}
		//junk data used for alignment
		else if (identifier == "JUNK") {
			//Skip the chunk data
			this.SkipExtraData(chunkByteSize);
		}
		else {
#if DEBUG
			Console.WriteLine($"Unknown chunk identifier: {identifier} (size: {chunkByteSize})");
#endif

			//Skip the chunk data
			this.SkipExtraData(chunkByteSize);
		}
	}

	private void ParseFullFile() {
		int waveChunkSize = this.ParseHeader();

		int sampleCount = 0;

		//Read all chunks in the file
		while (this._stream.Position + 4 < this._stream.Length) {
			this.ReadChunk(ref sampleCount);
		}

		if (this._dataChunks.Count == 0)
			throw new InvalidDataException("No data chunks found in file!");

		//Seek to the start of the first data chunk
		this._stream.Position = this._dataChunks[0].FileOffset;
	}

	public void Dispose() {
		this._reader.Dispose();

		if (!this._leaveOpen)
			this._stream.Close();
	}
}
