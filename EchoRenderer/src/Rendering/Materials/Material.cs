﻿using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using CodeHelpers;
using CodeHelpers.Mathematics;
using EchoRenderer.Common;
using EchoRenderer.Mathematics;
using EchoRenderer.Mathematics.Primitives;
using EchoRenderer.Rendering.Memory;
using EchoRenderer.Textures;

namespace EchoRenderer.Rendering.Materials
{
	// using static Utilities;

	public abstract class Material
	{
		NotNull<Texture> _albedo = Texture.black;
		NotNull<Texture> _normal = Texture.normal;

		/// <summary>
		/// The primary color of this <see cref="Material"/>.
		/// </summary>
		public Texture Albedo
		{
			get => _albedo;
			set => _albedo = value;
		}

		/// <summary>
		/// The local normal direction deviation of this <see cref="Material"/>.
		/// </summary>
		public Texture Normal
		{
			get => _normal;
			set => _normal = value;
		}

		/// <summary>
		/// The emission of this <see cref="Material"/>.
		/// </summary>
		public Float3 Emission { get; set; }

		/// <summary>
		/// The intensity of <see cref="Normal"/> on this <see cref="Material"/>.
		/// </summary>
		public Float3 NormalIntensity { get; set; } = Float3.one;

		bool zeroEmission;
		bool zeroNormal;

		Vector128<float> normalIntensityVector;

		static Vector128<float> NormalShift => Vector128.Create(-1f, -1f, -2f, 0f);

		/// <summary>
		/// Invoked before a new render session begins; can be used to execute any kind of preprocessing work for this <see cref="Material"/>.
		/// NOTE: invoking any of the rendering related methods prior to invoking this method after a change will result in undefined behaviors!
		/// </summary>
		public virtual void Prepare()
		{
			zeroEmission = !Emission.PositiveRadiance();
			zeroNormal = Normal == Texture.normal || NormalIntensity == Float3.zero;
			normalIntensityVector = Utilities.ToVector(NormalIntensity);
		}

		/// <summary>
		/// Determines the scattering properties of this material at <paramref name="interaction"/>
		/// and potentially initializes the appropriate properties in <paramref name="interaction"/>.
		/// </summary>
		public abstract void Scatter(ref Interaction interaction, Arena arena);

		/// <summary>
		/// Applies this <see cref="Material"/>'s <see cref="Normal"/> mapping at <paramref name="texcoord"/>
		/// to <paramref name="normal"/>. Returns whether this method caused <paramref name="normal"/> to change.
		/// </summary>
		public bool ApplyNormalMapping(in Float2 texcoord, ref Float3 normal)
		{
			if (zeroNormal) return false;

			//Evaluate normal texture at texcoord
			Vector128<float> local = PackedMath.Clamp01(Normal[texcoord]);
			local = PackedMath.FMA(local, Vector128.Create(2f), NormalShift);

			local = Sse.Multiply(local, normalIntensityVector);
			if (PackedMath.AlmostZero(local)) return false;

			//Create transform to move from local direction to world space based
			NormalTransform transform = new NormalTransform(normal);
			Float3 delta = transform.LocalToWorld(Utilities.ToFloat3(local));

			normal = (normal - delta).Normalized;
			return true;
		}

		/// <summary>
		/// Samples <paramref name="texture"/> at <paramref name="interaction"/> as a <see cref="Float4"/>.
		/// </summary>
		protected static Float4 Sample(Texture texture, in Interaction interaction) => Utilities.ToFloat4(texture[interaction.shade.Texcoord]);
	}
}