using System;

namespace Task.Core;

/// <summary>
/// Implementation of the IUid interface for generating unique identifiers (UIDs) for tasks. This class provides a method to generate a UID by creating a new GUID and taking the first 6 characters of its string representation. This approach ensures that each UID is unique while keeping it short and manageable.
/// </summary>
public class Uid : IUid
{
	public string GenerateUid()
	{
		return Guid.NewGuid().ToString()[..6];
	}
}

/// <summary>
/// Interface for generating unique identifiers (UIDs) for tasks. This allows for flexibility in how UIDs are generated, enabling different implementations if needed (e.g., using a different format or source for UIDs). The default implementation generates a 6-character UID based on a GUID.
/// </summary>
public interface IUid
{
	string GenerateUid();
}