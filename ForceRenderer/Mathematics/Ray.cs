﻿using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using CodeHelpers.Mathematics;

namespace ForceRenderer.Mathematics
{
	[StructLayout(LayoutKind.Explicit, Size = 48)]
	public readonly struct Ray
	{
		/// <summary>
		/// Constructs a ray.
		/// </summary>
		/// <param name="origin">The origin of the ray</param>
		/// <param name="direction">The direction of the ray. NOTE: it should be normalized.</param>
		/// <param name="forwardShift">Whether you want to create the ray so it is shifted a bit forward to avoid intersection with itself.</param>
		public Ray(Float3 origin, Float3 direction, bool forwardShift) : this
		(
			forwardShift ? origin + direction * 5E-4f : origin,
			direction
		) { }

		/// <summary>
		/// Constructs a ray.
		/// </summary>
		/// <param name="origin">The origin of the ray</param>
		/// <param name="direction">The direction of the ray. NOTE: it should be normalized.</param>
		public Ray(Float3 origin, Float3 direction)
		{
			Debug.Assert(Scalars.AlmostEquals(direction.SquaredMagnitude, 1f));

			originVector = default;
			directionVector = default;

			this.origin = origin;
			this.direction = direction;

			Vector128<float> reciprocalVector = Sse.Divide(oneVector, directionVector); //Because _mm_rcp_ps is only an approximation, we cannot use it here
			inverseDirectionVector = Sse.Min(maxValueVector, Sse.Max(minValueVector, reciprocalVector));

			Vector128<float> negated = Sse.Subtract(Vector128<float>.Zero, inverseDirectionVector);
			absolutedInverseDirectionVector = Sse.Max(negated, inverseDirectionVector);
		}

		[FieldOffset(0)] public readonly Float3 origin;
		[FieldOffset(12)] public readonly Float3 direction;

		//NOTE: these fields have overlapping memory offsets to reduce footprint. Pay extra attention when assigning them.
		[FieldOffset(0)] public readonly Vector128<float> originVector;
		[FieldOffset(12)] public readonly Vector128<float> directionVector;
		[FieldOffset(24)] public readonly Vector128<float> inverseDirectionVector;
		[FieldOffset(36)] public readonly Vector128<float> absolutedInverseDirectionVector;

		static readonly Vector128<float> minValueVector = Vector128.Create(float.MinValue, float.MinValue, float.MinValue, float.MinValue);
		static readonly Vector128<float> maxValueVector = Vector128.Create(float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue);
		static readonly Vector128<float> oneVector = Vector128.Create(1f, 1f, 1f, 1f);

		public Float3 GetPoint(float distance) => origin + direction * distance;

		public override string ToString() => $"{nameof(origin)}: {origin}, {nameof(direction)}: {direction}";
	}
}