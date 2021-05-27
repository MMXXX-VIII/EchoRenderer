using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using CodeHelpers;
using CodeHelpers.Files;
using CodeHelpers.Mathematics;
using EchoRenderer.IO;
using EchoRenderer.Mathematics;

namespace EchoRenderer.Textures
{
	/// <summary>
	/// The default texture; stores RGBA color information with 32 bits per channel, supports full float range.
	/// Saving and loading from image files handled by <see cref="Bitmap"/>. Can be offloaded to separate threads.
	/// </summary>
	public class Texture2D : Texture, ILoadableAsset
	{
		public Texture2D(Int2 size) : base(size) => pixels = new Vector128<float>[size.Product];

		readonly Vector128<float>[] pixels;

		public override ref Vector128<float> this[int index] => ref pixels[index];

		static readonly ReadOnlyCollection<string> _acceptableFileExtensions = new(new[] {".png", ".jpg", ".tiff", ".bmp", ".gif", ".exif", FloatingPointImageExtension});
		static readonly ReadOnlyCollection<ImageFormat> compatibleFormats = new(new[] {ImageFormat.Png, ImageFormat.Jpeg, ImageFormat.Tiff, ImageFormat.Bmp, ImageFormat.Gif, ImageFormat.Exif, null});

		const string FloatingPointImageExtension = ".fpi";
		IReadOnlyList<string> ILoadableAsset.AcceptableFileExtensions => _acceptableFileExtensions;

		public void Save(string relativePath, bool sRGB = true)
		{
			//Get path
			string extension = Path.GetExtension(relativePath);
			int extensionIndex;

			if (string.IsNullOrEmpty(extension))
			{
				extensionIndex = 0;
				relativePath = Path.ChangeExtension(relativePath, _acceptableFileExtensions[0]);
			}
			else
			{
				extensionIndex = _acceptableFileExtensions.IndexOf(extension);
				if (extensionIndex < 0) throw ExceptionHelper.Invalid(nameof(relativePath), relativePath, "does not have a compatible extension!");
			}

			string path = AssetsUtility.GetAssetsPath(relativePath);

			if (extension == FloatingPointImageExtension)
			{
				SaveFloatingPointImage(path);
				return;
			}

			//Export
			using Bitmap bitmap = new Bitmap(size.x, size.y);

			Rectangle rectangle = new Rectangle(0, 0, size.x, size.y);
			BitmapData bits = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

			unsafe
			{
				byte* origin = (byte*)bits.Scan0;
				Parallel.For(0, length, SaveARGB);

				void SaveARGB(int index)
				{
					Vector128<float> vector = this[index];
					if (sRGB) vector = Sse.Sqrt(vector);

					byte* pointer = origin + index * 4;
					Color32 color = (Color32)Utilities.ToFloat4(ref vector);

					pointer[0] = color.b;
					pointer[1] = color.g;
					pointer[2] = color.r;
					pointer[3] = color.a;
				}
			}

			bitmap.UnlockBits(bits);
			bitmap.Save(path, compatibleFormats[extensionIndex]);
		}

		public static Texture2D Load(string path, bool sRGB = true)
		{
			path = ((Texture2D)white).GetAbsolutePath(path);

			if (Path.GetExtension(path) == FloatingPointImageExtension) return ReadFloatingPointImage(path);

			using Bitmap source = new Bitmap(path, true);
			PixelFormat format = source.PixelFormat;
			Int2 size = new Int2(source.Width, source.Height);

			Texture2D texture = new Texture2D(size);

			Rectangle rectangle = new Rectangle(0, 0, texture.size.x, texture.size.y);
			BitmapData data = source.LockBits(rectangle, ImageLockMode.ReadOnly, format);

			unsafe
			{
				byte* origin = (byte*)data.Scan0;

				switch (Image.GetPixelFormatSize(format))
				{
					case 24:
					{
						Parallel.For(0, texture.length, LoadRGB);
						break;
					}
					case 32:
					{
						Parallel.For(0, texture.length, LoadARGB);
						break;
					}
					default: throw ExceptionHelper.Invalid(nameof(format), format, "is not an acceptable format!");
				}

				void LoadRGB(int index)
				{
					ref var target = ref texture[index];
					byte* pointer = origin + 3 * index;

					Color32 pixel = new Color32(pointer[2], pointer[1], pointer[0]);
					Vector128<float> vector = Utilities.ToVector((Float4)pixel);

					target = sRGB ? Sse.Multiply(vector, vector) : vector;
				}

				void LoadARGB(int index)
				{
					ref var target = ref texture[index];
					byte* pointer = origin + 4 * index;

					Color32 pixel = new Color32(pointer[2], pointer[1], pointer[0], pointer[3]);
					Vector128<float> vector = Utilities.ToVector((Float4)pixel);

					target = sRGB ? Sse.Multiply(vector, vector) : vector;
				}
			}

			source.UnlockBits(data);
			return texture;
		}

		void SaveFloatingPointImage(string path)
		{
			using Stream stream = new GZipStream(File.Open(path, FileMode.Create), CompressionLevel.Optimal);
			using DataWriter writer = new DataWriter(stream);

			writer.Write(1); //Writes version number
			Write(writer);
		}

		static Texture2D ReadFloatingPointImage(string path)
		{
			using Stream stream = new GZipStream(File.Open(path, FileMode.Open), CompressionMode.Decompress);
			using DataReader reader = new DataReader(stream);

			int version = reader.ReadInt32(); //Reads version number
			if (version == 0) return ReadRaw(reader);

			return Read(reader);
		}

		public unsafe void Write(DataWriter writer)
		{
			writer.WriteCompact(size);

			var sequence = Vector128<uint>.Zero;

			for (int i = 0; i < length; i++)
			{
				Vector128<uint> current = this[i].AsUInt32();
				Vector128<uint> xor = Sse2.Xor(sequence, current);

				//Write the xor difference as variable length quantity for lossless compression

				sequence = current;
				uint* pointer = (uint*)&xor;

				for (int j = 0; j < 4; j++)
				{
					uint bits = pointer[j];
					writer.WriteCompact(bits);
				}
			}
		}

		public static unsafe Texture2D Read(DataReader reader)
		{
			Int2 size = reader.ReadInt2Compact();
			Texture2D texture = new Texture2D(size);

			var sequence = Vector128<uint>.Zero;
			uint* read = stackalloc uint[4];

			//Read the xor difference sequence

			for (int i = 0; i < texture.length; i++)
			{
				for (int j = 0; j < 4; j++)
				{
					read[j] = reader.ReadUInt32Compact();
				}

				Vector128<uint> xor = *(Vector128<uint>*)read;
				Vector128<uint> current = Sse2.Xor(sequence, xor);

				texture[i] = current.AsSingle();
				sequence = current;
			}

			return texture;
		}

		static Texture2D ReadRaw(DataReader reader)
		{
			Int2 size = reader.ReadInt2();
			Texture2D texture = new Texture2D(size);

			for (int i = 0; i < texture.length; i++) texture[i] = Utilities.ToVector(reader.ReadFloat4());

			return texture;
		}

		public override void CopyFrom(Texture texture)
		{
			if (texture is not Texture2D texture2D) base.CopyFrom(texture);
			else Array.Copy(texture2D.pixels, pixels, length);
		}
	}
}