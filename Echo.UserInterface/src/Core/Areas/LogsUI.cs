﻿using System.IO;
using CodeHelpers.Diagnostics;
using Echo.UserInterface.Backend;
using ImGuiNET;

namespace Echo.UserInterface.Core.Areas;

public class LogsUI : AreaUI
{
	public LogsUI() : base("Logs") { }

	public override void Initialize()
	{
		base.Initialize();
		DebugHelper.Logger = LogList.Logger;
	}

	protected override void Update(in Moment moment)
	{
		if (ImGui.Button("Clear All")) LogList.Clear();

		ImGui.SameLine();
		if (ImGui.Button("Save to File")) SaveToFile();

		ImGui.Separator();
		ImGui.BeginChild("History");

		foreach (string log in LogList.Logs) ImGui.TextUnformatted(log);

		ImGui.EndChild();
	}

	static void SaveToFile() => ActionQueue.Enqueue("Log List Dump", () =>
	{
		using var writer = new StreamWriter("logs.txt");
		foreach (string log in LogList.Logs) writer.WriteLine(log);
	});
}