﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using CodeHelpers.Mathematics;
using EchoRenderer.Mathematics.Accelerators;
using EchoRenderer.Rendering;

namespace EchoRenderer.Mathematics.Intersections
{
	/// <summary>
	/// Query for the traverse of a <see cref="Ray"/> to find out whether an intersection with a <see cref="PressedScene"/>
	/// exists, and if it does, the exact distance of that intersections and other specific information about it.
	/// </summary>
	public struct TraceQuery
	{
		public TraceQuery(in Ray ray, float distance = float.PositiveInfinity, in GeometryToken ignore = default)
		{
			this.ray = ray;
			this.ignore = ignore;

			current = default;
			Unsafe.SkipInit(out token);
			this.distance = distance;
			Unsafe.SkipInit(out uv);

#if DEBUG
			originalDistance = distance;
#endif
		}

		/// <summary>
		/// The main ray of this <see cref="TraceQuery"/>. Note that this will be changed and updated
		/// as we traverse through the scene when we go from a parent coordinate system to a child system.
		/// </summary>
		public Ray ray;

		/// <summary>
		/// The <see cref="GeometryToken"/> that represents a geometry that this <see cref="TraceQuery"/> should ignore
		/// if we come in very close (<see cref="PressedPack.DistanceMin"/>) contact with it. This should mainly be assigned
		/// to the <see cref="GeometryToken"/> of the previous <see cref="TraceQuery"/> to avoid origin intersections.
		/// </summary>
		public readonly GeometryToken ignore;

		/// <summary>
		/// Used during intersection test; undefined after the test is concluded (do not use upon completion).
		/// Records the intermediate instancing layers as the query travels through the scene and geometries.
		/// </summary>
		public GeometryToken current;

		/// <summary>
		/// After tracing completes, this field will be assigned the <see cref="GeometryToken"/>
		/// of the intersected surface. NOTE: if no intersection occurs, this field is undefined.
		/// </summary>
		public GeometryToken token;

		/// <summary>
		/// After tracing completes, this field will be assigned the distance of the intersection to the <see cref="ray"/> origin.
		/// NOTE: if there is no intersection with the scene, this field will be assigned <see cref="float.PositiveInfinity"/>.
		/// </summary>
		public float distance;

		/// <summary>
		/// After tracing completes, this field will be assigned the local position/coordinate on the specific
		/// parametrization of the intersected surface. NOTE: if no intersection occurs, this field is undefined.
		/// </summary>
		public Float2 uv;

#if DEBUG
		/// <summary>
		/// The immutable original distance to determine whether this <see cref="TraceQuery"/> actually <see cref="Hit"/> something.
		/// </summary>
		readonly float originalDistance;

		/// <summary>
		/// Returns whether this <see cref="TraceQuery"/> has intersected with anything.
		/// </summary>
		public readonly bool Hit => distance < originalDistance;
#endif

		/// <summary>
		/// Spawns and returns a new <see cref="TraceQuery"/> from the result of this <see cref="TraceQuery"/> towards <paramref name="direction"/>.
		/// </summary>
		public readonly TraceQuery Next(in Float3 direction)
		{
			AssertHit();
			return new TraceQuery(new Ray(ray.GetPoint(distance), direction), float.PositiveInfinity, token);
		}

		/// <summary>
		/// Spans and returns a new <see cref="TraceQuery"/> with the same direction from the result of this <see cref="TraceQuery"/>.
		/// </summary>
		public readonly TraceQuery Next() => Next(ray.direction);

		/// <summary>
		/// Spawns and returns a new <see cref="OccludeQuery"/> from the result of this <see cref="TraceQuery"/> towards <paramref name="direction"/>.
		/// </summary>
		public readonly OccludeQuery Occlude(in Float3 direction, float travel = float.PositiveInfinity)
		{
			AssertHit();
			return new OccludeQuery(new Ray(ray.GetPoint(distance), direction), travel, token);
		}

		/// <summary>
		/// Spans and returns a new <see cref="OccludeQuery"/> with the same direction from the result of this <see cref="TraceQuery"/>.
		/// </summary>
		public readonly OccludeQuery Occlude(float travel = float.PositiveInfinity) => Occlude(ray.direction, travel);

		/// <summary>
		/// Ensures that this <see cref="TraceQuery"/> has <see cref="Hit"/> something.
		/// </summary>
		[Conditional("DEBUG")]
		public readonly void AssertHit()
		{
#if DEBUG
			CodeHelpers.Diagnostics.Assert.IsTrue(Hit);
#endif
		}

		public static implicit operator TraceQuery(in Ray ray) => new(ray);
	}
}