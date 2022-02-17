﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using CodeHelpers.Diagnostics;
using CodeHelpers.Mathematics;

namespace EchoRenderer.Common.Mathematics.Primitives;

/// <summary>
/// A 3D box that is aligned to the coordinate axes, usually used to bound other objects.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public readonly struct AxisAlignedBoundingBox
{
	public AxisAlignedBoundingBox(in Float3 min, in Float3 max)
	{
		Unsafe.SkipInit(out minV);
		Unsafe.SkipInit(out maxV);

		this.min = min;
		this.max = max;

		Assert.IsTrue(max >= min);
	}

	public AxisAlignedBoundingBox(ReadOnlySpan<Float3> points)
	{
		Unsafe.SkipInit(out minV);
		Unsafe.SkipInit(out maxV);

		min = Float3.positiveInfinity;
		max = Float3.negativeInfinity;

		foreach (ref readonly Float3 point in points)
		{
			min = min.Min(point);
			max = max.Max(point);
		}

		Assert.IsTrue(max >= min);
	}

	public AxisAlignedBoundingBox(ReadOnlySpan<AxisAlignedBoundingBox> aabbs)
	{
		Unsafe.SkipInit(out min);
		Unsafe.SkipInit(out max);

		minV = Vector128.Create(float.PositiveInfinity);
		maxV = Vector128.Create(float.NegativeInfinity);

		foreach (ref readonly AxisAlignedBoundingBox aabb in aabbs)
		{
			minV = Sse.Min(minV, aabb.minV);
			maxV = Sse.Max(maxV, aabb.maxV);
		}

		Assert.IsTrue(max >= min);
	}

	AxisAlignedBoundingBox(in Vector128<float> minV, in Vector128<float> maxV)
	{
		Unsafe.SkipInit(out min);
		Unsafe.SkipInit(out max);

		this.minV = minV;
		this.maxV = maxV;

		Assert.IsTrue(max >= min);
	}

	public static readonly AxisAlignedBoundingBox none = new(Float3.positiveInfinity, Float3.positiveInfinity);

	[FieldOffset(00)] public readonly Float3 min;
	[FieldOffset(16)] public readonly Float3 max;

	[FieldOffset(00)] readonly Vector128<float> minV;
	[FieldOffset(16)] readonly Vector128<float> maxV;

	public Float3 Center => Utilities.ToFloat3(Sse.Multiply(Sse.Add /**/(maxV, minV), Vector128.Create(0.5f)));
	public Float3 Extend => Utilities.ToFloat3(Sse.Multiply(Sse.Subtract(maxV, minV), Vector128.Create(0.5f)));

	public float Area
	{
		get
		{
			Float3 size = Utilities.ToFloat3(Sse.Subtract(maxV, minV));
			return size.x * size.y + size.x * size.z + size.y * size.z;
		}
	}

	public int MajorAxis => (max - min).MaxIndex;

	/// <summary>
	/// Multiplier used on the far distance to remove floating point arithmetic errors when calculating
	/// an intersection with <see cref="AxisAlignedBoundingBox"/> by converting false-misses into false-hits.
	/// See https://www.arnoldrenderer.com/research/jcgt2013_robust_BVH-revised.pdf for details.
	/// </summary>
	public const float FarMultiplier = 1.00000024f;

	/// <summary>
	/// Tests intersection with bounding box. Returns distance to the nearest intersection point.
	/// NOTE: return can be negative, which means the ray origins inside this box.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public unsafe float Intersect(in Ray ray)
	{
		//The well known 'slab method'. Referenced from https://tavianator.com/2011/ray_box.html

		Vector128<float> lengths0 = Sse.Multiply(Sse.Subtract(minV, ray.originV), ray.inverseDirectionV);
		Vector128<float> lengths1 = Sse.Multiply(Sse.Subtract(maxV, ray.originV), ray.inverseDirectionV);

		Vector128<float> lengthsMin = Sse.Max(lengths0, lengths1);
		Vector128<float> lengthsMax = Sse.Min(lengths0, lengths1);

		//The previous two lines could be a bit confusing: we are trying to find
		//the min of the max lengths for far and the max of the min lengths for near

		float far;
		float near;

		//Compute horizontal min and max

		if (Avx.IsSupported)
		{
			//Permute vector for min max, ignore last component
			Vector128<float> minPermute = Avx.Permute(lengthsMin, 0b0100_1010);
			Vector128<float> maxPermute = Avx.Permute(lengthsMax, 0b0100_1010);

			lengthsMin = Sse.Min(lengthsMin, minPermute);
			lengthsMax = Sse.Max(lengthsMax, maxPermute);

			//Second permute for min max
			minPermute = Avx.Permute(lengthsMin, 0b1011_0001);
			maxPermute = Avx.Permute(lengthsMax, 0b1011_0001);

			lengthsMin = Sse.Min(lengthsMin, minPermute);
			lengthsMax = Sse.Max(lengthsMax, maxPermute);

			//Extract result
			far = *(float*)&lengthsMin;
			near = *(float*)&lengthsMax;
		}
		else
		{
			//Software implementation
			far = (*(Float3*)&lengthsMin).MinComponent;
			near = (*(Float3*)&lengthsMax).MaxComponent;
		}

		far *= FarMultiplier;

		//NOTE: we place the infinity constant as the second return candidate because
		//if either near or far is NaN, this method will still return a valid result
		return far >= near && far >= 0f ? near : float.PositiveInfinity;
	}

	public AxisAlignedBoundingBox Encapsulate(in AxisAlignedBoundingBox other) => new
	(
		Sse.Min(minV, other.minV),
		Sse.Max(maxV, other.maxV)
	);

	public override int GetHashCode() => unchecked((min.GetHashCode() * 397) ^ max.GetHashCode());
	public override string ToString() => $"{nameof(Center)}: {Center}, {nameof(Extend)}: {Extend}";
}