using System;
using System.Collections.Generic;

namespace AbyssMod.Services;

public class FieldEntry
{
	public string Name { get; init; }

	public IntPtr FieldPtr { get; init; }

	public int Offset { get; init; }

	public List<FieldRule> Rules { get; init; } = new List<FieldRule>();
}
