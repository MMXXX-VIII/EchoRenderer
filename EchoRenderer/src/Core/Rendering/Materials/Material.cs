﻿using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using CodeHelpers;
using CodeHelpers.Mathematics;
using EchoRenderer.Common;
using EchoRenderer.Common.Mathematics;
using EchoRenderer.Common.Mathematics.Primitives;
using EchoRenderer.Common.Memory;
using EchoRenderer.Core.Aggregation.Primitives;
using EchoRenderer.Core.Rendering.Scattering;
using EchoRenderer.Core.Texturing;

namespace EchoRenderer.Core.Rendering.Materials;

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
	/// The intensity of <see cref="Normal"/> on this <see cref="Material"/>.
	/// </summary>
	public Float3 NormalIntensity { get; set; } = Float3.one;

	bool zeroNormal;

	Vector128<float> normalIntensityV;

	static Vector128<float> NormalShiftV => Vector128.Create(-1f, -1f, -2f, 0f);

	/// <summary>
	/// Invoked before a new render session begins; can be used to execute any kind of preprocessing work for this <see cref="Material"/>.
	/// NOTE: invoking any of the rendering related methods prior to invoking this method after a change will result in undefined behaviors!
	/// </summary>
	public virtual void Prepare()
	{
		zeroNormal = Normal == Texture.normal || NormalIntensity == Float3.zero;
		normalIntensityV = Utilities.ToVector(NormalIntensity);
	}

	/// <summary>
	/// Determines the scattering properties of this material at <paramref name="touch"/>
	/// and potentially initializes the appropriate properties in <paramref name="touch"/>.
	/// </summary>
	public abstract void Scatter(ref Touch touch, Allocator allocator);

	/// <summary>
	/// Applies this <see cref="Material"/>'s <see cref="Normal"/> mapping at <paramref name="texcoord"/>
	/// to <paramref name="normal"/>. Returns whether this method caused <paramref name="normal"/> to change.
	/// </summary>
	public bool ApplyNormalMapping(in Float2 texcoord, ref Float3 normal)
	{
		if (zeroNormal) return false;

		//Evaluate normal texture at texcoord
		Vector128<float> local = PackedMath.Clamp01(Normal[texcoord]);
		local = PackedMath.FMA(local, Vector128.Create(2f), NormalShiftV);

		local = Sse.Multiply(local, normalIntensityV);
		if (PackedMath.AlmostZero(local)) return false;

		//Create transform to move from local direction to world space based
		NormalTransform transform = new NormalTransform(normal);
		Float3 delta = transform.LocalToWorld(Utilities.ToFloat3(local));

		normal = (normal - delta).Normalized;
		return true;
	}

	/// <summary>
	/// Samples <paramref name="texture"/> at <paramref name="touch"/> as a <see cref="Float4"/>.
	/// </summary>
	protected static Float4 Sample(Texture texture, in Touch touch) => Utilities.ToFloat4(texture[touch.shade.Texcoord]);

	/// <summary>
	/// A wrapper struct used to easily create <see cref="BSDF"/> and add <see cref="BxDF"/> to it.
	/// </summary>
	protected readonly struct MakeBSDF
	{
		public MakeBSDF(ref Touch touch, Allocator allocator)
		{
			this.allocator = allocator;
			bsdf = allocator.New<BSDF>();

			touch.bsdf = bsdf;
			bsdf.Reset(touch);
		}

		readonly Allocator allocator;
		readonly BSDF bsdf;

		/// <summary>
		/// Adds a new <see cref="BxDF"/> of type <typeparamref name="T"/> to <see cref="Touch.bsdf"/> and returns it.
		/// </summary>
		public T Add<T>() where T : BxDF, new()
		{
			T function = allocator.New<T>();

			bsdf.Add(function);
			return function;
		}
	}
}