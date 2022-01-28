﻿using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using CodeHelpers.Mathematics;
using EchoRenderer.Common;
using EchoRenderer.Mathematics;
using EchoRenderer.PostProcess.Operators;
using EchoRenderer.Textures.Grid;

namespace EchoRenderer.PostProcess
{
	public class Bloom : PostProcessingWorker
	{
		public Bloom(PostProcessingEngine engine) : base(engine) => deviation = renderBuffer.LogSize / 64f;

		public float Intensity { get; set; } = 0.88f;
		public float Threshold { get; set; } = 0.95f;

		readonly float deviation;
		ArrayGrid workerBuffer;

		public override void Dispatch()
		{
			//Allocate blur resources
			using var handle = FetchTemporaryBuffer(out workerBuffer);
			using var blur = new GaussianBlur(this, workerBuffer, deviation, 6);

			//Fill luminance threshold to workerBuffer
			RunPass(LuminancePass);

			//Run Gaussian blur on workerBuffer
			blur.Run();

			//Final pass to combine blurred workerBuffer with renderBuffer
			RunPass(CombinePass);
		}

		void LuminancePass(Int2 position)
		{
			Vector128<float> source = renderBuffer[position];
			float luminance = PackedMath.GetLuminance(source);

			float brightness = luminance - Threshold;
			Vector128<float> result = Vector128<float>.Zero;

			if (brightness > 0f && !FastMath.AlmostZero(luminance))
			{
				float multiplier = brightness / luminance * Intensity;
				result = Sse.Multiply(source, Vector128.Create(multiplier));
			}

			workerBuffer[position] = result;
		}

		void CombinePass(Int2 position)
		{
			Vector128<float> source = workerBuffer[position];
			Vector128<float> target = renderBuffer[position];

			renderBuffer[position] = Sse.Add(target, source);
		}
	}
}