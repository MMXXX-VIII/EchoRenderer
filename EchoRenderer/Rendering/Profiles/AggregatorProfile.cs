using System;
using System.Collections.Generic;
using CodeHelpers;
using EchoRenderer.Mathematics.Intersections;
using EchoRenderer.Mathematics.Primitives;

namespace EchoRenderer.Rendering.Profiles
{
	public record AggregatorProfile : IProfile
	{
		/// <summary>
		/// Explicitly indicate the type of <see cref="Aggregator"/> to use.
		/// This can be left as null for automatic accelerator determination.
		/// </summary>
		public Type AggregatorType { get; init; }

		public void Validate()
		{
			if (AggregatorType?.IsSubclassOf(typeof(Aggregator)) == false) throw ExceptionHelper.Invalid(nameof(AggregatorType), AggregatorType, $"is not of type {nameof(Aggregator)}");
		}

		public Aggregator CreateAccelerator(PressedPack pack, IReadOnlyList<AxisAlignedBoundingBox> aabbs, IReadOnlyList<uint> tokens)
		{
			if (AggregatorType == typeof(LinearAggregator)) return new LinearAggregator(pack, aabbs, tokens);
			if (AggregatorType == typeof(BoundingVolumeHierarchy)) return new BoundingVolumeHierarchy(pack, aabbs, tokens);
			if (AggregatorType == typeof(QuadBoundingVolumeHierarchy)) return new QuadBoundingVolumeHierarchy(pack, aabbs, tokens);

			return new QuadBoundingVolumeHierarchy(pack, aabbs, tokens);
		}
	}
}