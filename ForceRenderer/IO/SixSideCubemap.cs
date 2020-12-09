﻿using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CodeHelpers;
using CodeHelpers.Vectors;

namespace ForceRenderer.IO
{
	public class SixSideCubemap : Cubemap
	{
		public SixSideCubemap(string path)
		{
			var names = IndividualTextureNames;
			Texture[] sources = new Texture[names.Count];

			Exception error = null;

			Parallel.For
			(
				0, names.Count, (index, state) =>
								{
									try
									{
										sources[index] = new Texture(Path.Combine(path, names[index]));
									}
									catch (FileNotFoundException exception)
									{
										error = exception;
										state.Break();
									}
								}
			);

			if (error != null) throw error;
			textures = new ReadOnlyCollection<Texture>(sources);
		}

		readonly ReadOnlyCollection<Texture> textures;

		public static readonly ReadOnlyCollection<string> IndividualTextureNames = new ReadOnlyCollection<string>(new[] {"px", "py", "pz", "nx", "ny", "nz"});

		public override Color32 Sample(Float3 direction)
		{
			Float3 absoluted = direction.Absoluted;
			int index = absoluted.MaxIndex;

			float component = direction[index];
			if (direction[index] < 0f) index += 3;

			Float2 uv = index switch
						{
							0 => new Float2(-direction.z, direction.y),
							1 => new Float2(direction.x, -direction.z),
							2 => direction.XY,
							3 => direction.ZY,
							4 => direction.XZ,
							_ => new Float2(-direction.x, direction.y)
						};

			component = Math.Abs(component) * 2f;
			return Sample(index, uv / component);
		}

		/// <summary>
		/// Samples a specific bitmap at <paramref name="uv"/>.
		/// <paramref name="uv"/> is between -0.5 to 0.5 with zero in the middle.
		/// </summary>
		Color32 Sample(int index, Float2 uv) => textures[index].GetPixel(uv + Float2.half);
	}
}