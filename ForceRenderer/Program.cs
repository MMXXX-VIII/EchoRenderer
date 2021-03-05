﻿using System;
using System.Threading;
using CodeHelpers;
using CodeHelpers.Diagnostics;
using CodeHelpers.Mathematics;
using CodeHelpers.Threads;
using ForceRenderer.Rendering;
using ForceRenderer.Rendering.PostProcessing;
using ForceRenderer.Terminals;
using ForceRenderer.Textures;

namespace ForceRenderer
{
	public class Program
	{
		static void Main()
		{
			Texture2D texture = Texture2D.Load("render.fpi");
			using var engine = new PostProcessingEngine(texture);

			engine.AddWorker(new BloomWorker(engine));

			PerformanceTest bloomTest = new PerformanceTest();

			using (bloomTest.Start())
			{
				engine.Dispatch();
				engine.WaitForProcess();
			}

			DebugHelper.Log($"Bloom used {bloomTest.ElapsedMilliseconds}ms");

			//Bloom used 15408.4506ms
			texture.Save("render.png");

			return;

			using Terminal terminal = new Terminal();
			renderTerminal = terminal;

			commandsController = new CommandsController(terminal);
			renderMonitor = new RenderMonitor(terminal);

			terminal.AddSection(commandsController);
			terminal.AddSection(renderMonitor);

			ThreadHelper.MainThread = Thread.CurrentThread;
			RandomHelper.Seed = 47;

			PerformRender();
			Console.ReadKey();
		}

		static RenderEngine renderEngine;
		static Terminal renderTerminal;

		static CommandsController commandsController;
		static RenderMonitor renderMonitor;

		static void PerformRender()
		{
			Int2[] resolutions =
			{
				new(480, 270), new(960, 540), new(1920, 1080),
				new(3840, 2160), new(1024, 1024), new(512, 512)
			};

			Texture2D buffer = new Texture2D(resolutions[1]);
			using RenderEngine engine = new RenderEngine
										{
											RenderBuffer = buffer, Scene = new RandomSpheresScene(40),
											PixelSample = 48, AdaptiveSample = 400, TileSize = 32
										};

			renderEngine = engine;
			renderMonitor.Engine = engine;

			PerformanceTest setupTest = new PerformanceTest();
			using (setupTest.Start()) engine.Begin();

			commandsController.Log($"Engine Setup Complete: {setupTest.ElapsedMilliseconds}ms");
			engine.WaitForRender();

			buffer.Save("render.fpi");

			using var postProcess = new PostProcessingEngine(buffer);

			postProcess.AddWorker(new BloomWorker(postProcess));
			postProcess.AddWorker(new ColorCorrectionWorker(postProcess, 1f));

			postProcess.Dispatch();
			postProcess.WaitForProcess();

			buffer.Save("render.png");

			//Logs render stats
			double elapsedSeconds = engine.Elapsed.TotalSeconds;
			long completedSample = engine.CompletedSample;

			commandsController.Log($"Completed after {elapsedSeconds:F2} seconds with {completedSample:N0} samples at {completedSample / elapsedSeconds:N0} samples per second.");
			renderEngine = null;
		}

		[Command]
		static CommandResult Pause()
		{
			if (renderEngine.CurrentState == RenderEngine.State.rendering)
			{
				renderEngine.Pause();
				return new CommandResult("Render pausing...", true);
			}

			return new CommandResult("Cannot pause: not rendering.", false);
		}

		[Command]
		static CommandResult Resume()
		{
			if (renderEngine.CurrentState == RenderEngine.State.paused)
			{
				renderEngine.Resume();
				return new CommandResult("Render resumed.", true);
			}

			return new CommandResult("Cannot resume: not paused.", false);
		}

		[Command]
		static CommandResult Abort()
		{
			if (renderEngine.Rendering)
			{
				renderEngine.Abort();
				return new CommandResult("Render aborted.", true);
			}

			return new CommandResult("Cannot abort: not rendering.", false);
		}

		[Command]
		static CommandResult RewriteTerminal()
		{
			renderTerminal.Rewrite = true;
			return new CommandResult("Terminal rewrote.", true);
		}
	}
}