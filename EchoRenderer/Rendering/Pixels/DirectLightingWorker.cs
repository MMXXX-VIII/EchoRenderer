using System;
using CodeHelpers.Mathematics;
using EchoRenderer.Mathematics.Intersections;
using EchoRenderer.Mathematics.Randomization;
using EchoRenderer.Objects.Lights;
using EchoRenderer.Rendering.Memory;
using EchoRenderer.Rendering.Profiles;
using EchoRenderer.Rendering.Distributions;

namespace EchoRenderer.Rendering.Pixels
{
	public class DirectLightingWorker : PixelWorker
	{
		public override Arena CreateArena(RenderProfile profile, uint seed)
		{
			Arena arena = base.CreateArena(profile, seed);
			arena.Random = new SystemRandom(seed);
			return arena;
		}

		public override Sample Render(Float2 uv, Arena arena)
		{
			RenderProfile profile = arena.profile;
			PressedScene scene = profile.Scene;

			Float3 radiance = Float3.zero;
			TraceQuery query = scene.camera.GetRay(uv);

			return scene.Trace(ref query) ? scene.Interact(query, out _).geometryNormal : Float3.zero;

			// if (!scene.Trace(ref query))
			// {
			// 	scene.Interact()
			// }

			throw new NotImplementedException();
		}

		protected override Distribution CreateDistribution(RenderProfile profile)
		{
			var distribution = new UniformDistribution(profile.TotalSample);

			for (int i = 0; i < profile.BounceLimit; i++)
			{
				foreach (Light light in profile.Scene.Lights)
				{
					int count = light.SampleCount;

					distribution.RequestSpanTwo(count); //Request span for light
					distribution.RequestSpanTwo(count); //Request span for bsdf
				}
			}

			return distribution;
		}
	}
}