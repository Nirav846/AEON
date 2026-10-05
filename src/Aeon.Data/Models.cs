using System.Text.Json.Serialization;

namespace Aeon.Data;

/// <summary>
/// One training "complex". Field names and nullability are taken from
/// schema/complex.schema.json - not guessed. Note that swap_equipment, also_suits,
/// also_suits_roles, pattern_id and supersedes are all optional and genuinely absent
/// on many records, so they are nullable rather than defaulted.
/// </summary>
public sealed record Complex
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("pattern_id")] public string? PatternId { get; init; }
    [JsonPropertyName("supersedes")] public int? Supersedes { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("sport")] public string Sport { get; init; } = "";
    [JsonPropertyName("role")] public string Role { get; init; } = "";
    [JsonPropertyName("category")] public string Category { get; init; } = "";
    [JsonPropertyName("focus")] public string Focus { get; init; } = "";
    [JsonPropertyName("execution")] public string Execution { get; init; } = "";
    [JsonPropertyName("swap")] public string Swap { get; init; } = "";
    [JsonPropertyName("equipment")] public List<string> Equipment { get; init; } = new();
    [JsonPropertyName("swap_equipment")] public List<string>? SwapEquipment { get; init; }
    [JsonPropertyName("also_suits")] public List<string>? AlsoSuits { get; init; }
    [JsonPropertyName("also_suits_roles")] public List<string>? AlsoSuitsRoles { get; init; }
    [JsonPropertyName("sources")] public List<string>? Sources { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = "";

    [JsonPropertyName("manual")] public Manual? Manual { get; init; }
}

public sealed record Manual
{
    [JsonPropertyName("why")] public string? Why { get; init; }
    [JsonPropertyName("cue")] public string? Cue { get; init; }
}

/// <summary>Circuit. From schema/circuit.schema.json. Note "Racquet Sports" is a
/// valid sport here but NOT in the complex enum - the two schemas disagree on
/// purpose, so Sport stays a plain string.</summary>
public sealed record Circuit
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("sport")] public string Sport { get; init; } = "";
    [JsonPropertyName("role")] public string Role { get; init; } = "";
    [JsonPropertyName("format")] public string Format { get; init; } = "";
    [JsonPropertyName("stations")] public List<Station> Stations { get; init; } = new();
    [JsonPropertyName("energy_system")] public string EnergySystem { get; init; } = "";
    [JsonPropertyName("coach_speak")] public string? CoachSpeak { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = "";
}

public sealed record Station
{
    [JsonPropertyName("execution")] public string Execution { get; init; } = "";
    [JsonPropertyName("ref_type")] public string? RefType { get; init; }
    [JsonPropertyName("ref_id")] public int? RefId { get; init; }
}

/// <summary>Conditioning protocol. From schema/conditioning.schema.json.</summary>
public sealed record ConditioningProtocol
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("sport")] public string Sport { get; init; } = "";
    [JsonPropertyName("modality")] public string Modality { get; init; } = "";
    [JsonPropertyName("work_to_rest")] public string WorkToRest { get; init; } = "";
    [JsonPropertyName("male_target")] public string? MaleTarget { get; init; }
    [JsonPropertyName("female_target")] public string? FemaleTarget { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = "";
}

/// <summary>
/// Concept. There is no schema/concept.schema.json in the repo - the authoritative
/// shape was read from data/concepts.jsonl itself. These records are a different
/// record type with no promote/reject lifecycle, which is why they carry neither
/// a `status` nor a `sport` field.
/// </summary>
public sealed record Concept
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("category")] public string Category { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("what")] public string What { get; init; } = "";
    [JsonPropertyName("why")] public string Why { get; init; } = "";
    [JsonPropertyName("cue")] public string Cue { get; init; } = "";
}

/// <summary>The four data arrays plus the build metadata, as written to
/// build/app-bundle.json by scripts/build_bundle.py.</summary>
public sealed record Bundle
{
    [JsonPropertyName("complexes")] public List<Complex> Complexes { get; init; } = new();
    [JsonPropertyName("circuits")] public List<Circuit> Circuits { get; init; } = new();
    [JsonPropertyName("conditioning")] public List<ConditioningProtocol> Conditioning { get; init; } = new();
    [JsonPropertyName("concepts")] public List<Concept> Concepts { get; init; } = new();
    [JsonPropertyName("version")] public string Version { get; init; } = "";
    [JsonPropertyName("generated_at")] public DateTimeOffset GeneratedAt { get; init; }

    [JsonIgnore]
    public int TotalRecords =>
        Complexes.Count + Circuits.Count + Conditioning.Count + Concepts.Count;
}

/// <summary>build/bundle-meta.json - the tiny manifest the app polls first.</summary>
public sealed record BundleMeta
{
    [JsonPropertyName("version")] public string Version { get; init; } = "";
    [JsonPropertyName("generated_at")] public DateTimeOffset GeneratedAt { get; init; }
    [JsonPropertyName("record_counts")] public Dictionary<string, int> RecordCounts { get; init; } = new();
}