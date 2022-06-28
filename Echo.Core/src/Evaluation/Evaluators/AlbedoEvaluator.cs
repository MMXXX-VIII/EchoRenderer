﻿using CodeHelpers.Packed;
using Echo.Core.Aggregation.Primitives;
using Echo.Core.Common.Memory;
using Echo.Core.Evaluation.Distributions.Continuous;
using Echo.Core.Evaluation.Materials;
using Echo.Core.Scenic.Preparation;
using Echo.Core.Textures.Colors;
using Echo.Core.Textures.Evaluation;

namespace Echo.Core.Evaluation.Evaluators;

public record AlbedoEvaluator : Evaluator
{
	public override IEvaluationLayer CreateOrClearLayer(RenderBuffer buffer) => CreateOrClearLayer<RGB128>(buffer, "albedo");

	public override Float4 Evaluate(PreparedSceneOld scene, in Ray ray, ContinuousDistribution distribution, Allocator allocator)
	{
		var query = new TraceQuery(ray);

		allocator.Restart();

		//Trace for intersection
		while (scene.Trace(ref query))
		{
			Touch touch = scene.Interact(query);

			Material material = touch.shade.material;
			material.Scatter(ref touch, allocator);

			if (touch.bsdf == null) query = query.SpawnTrace();
			else return (RGB128)material.SampleAlbedo(touch);
		}

		//Sample ambient
		return scene.lights.EvaluateAmbient(query.ray.direction);
	}
}