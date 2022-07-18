﻿using System;

namespace Echo.UserInterface.Backend;

public interface IApplication : IDisposable
{
	string Label { get; }

	TimeSpan UpdateDelay { get; }

	void Initialize(ImGuiDevice backend);

	void NewFrame(in Moment moment);
}