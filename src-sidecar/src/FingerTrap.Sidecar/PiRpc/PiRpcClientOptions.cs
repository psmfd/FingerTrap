namespace FingerTrap.Sidecar.PiRpc;

/// <summary>
/// Direction of one raw JSONL wire line relative to the sidecar, for
/// <see cref="PiRpcClientOptions.WireTap"/>.
/// </summary>
internal enum PiWireDirection
{
    /// <summary>Supervisor → child (a line written to pi's stdin).</summary>
    ToChild,

    /// <summary>Child → supervisor (a line read from pi's stdout).</summary>
    FromChild,
}

/// <summary>
/// Spawn and supervision configuration for one <see cref="PiRpcClient"/>.
/// The executable path and arguments are caller-supplied — resolution of
/// where pi lives (PATH, settings) stays with the caller, mirroring
/// <see cref="Pty.PtyService"/>'s injected-settings pattern; slice 1 never
/// resolves executables itself.
/// </summary>
internal sealed record PiRpcClientOptions
{
    public required string ExecutablePath { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = [];

    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Additive overrides applied on top of the inherited process
    /// environment. The child's environment is exactly
    /// <em>inherited + these overrides</em> and nothing else:
    /// <see cref="PiRpcClient"/> takes no credential dependency and never
    /// injects variables of its own — an invariant the conformance suite
    /// pins. Inheritance is deliberate: pi needs the operator's PATH,
    /// HOME, and provider keys to behave like an interactive session.
    /// </summary>
    public IReadOnlyDictionary<string, string>? EnvironmentOverrides { get; init; }

    /// <summary>
    /// Per-request timeout (docs/rpc-contract.md: the reference client
    /// budgets 30 s). Injectable so conformance tests assert expiry in
    /// milliseconds instead of half a minute.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long after stdin-EOF (the clean-shutdown trigger — flushes,
    /// exit 0) to wait before escalating.
    /// </summary>
    public TimeSpan EofGrace { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long after SIGTERM (exit 143, does NOT flush) to wait before
    /// SIGKILL. The reference client budgets 1 s; a protocol constant in
    /// spirit, tunable here if a real pi needs longer to die.
    /// </summary>
    public TimeSpan SigtermGrace { get; init; } = TimeSpan.FromSeconds(1);

    /// <inheritdoc cref="JsonlCodec.DefaultMaxLineBytes"/>
    public int MaxLineBytes { get; init; } = JsonlCodec.DefaultMaxLineBytes;

    /// <summary>
    /// Ring-buffer bound on captured stderr. Only the tail is ever useful
    /// for error enrichment, and pi reroutes every stray extension
    /// <c>console.log</c> to stderr — unbounded capture of a chatty child
    /// is a slow memory leak.
    /// </summary>
    public int MaxStderrBytes { get; init; } = 16 * 1024;

    /// <summary>
    /// Bounded event-channel capacity, wait-mode on full: a slow consumer
    /// stalls the read loop, the OS pipe fills, and the child's writes
    /// block — backpressure propagates to the child instead of being
    /// absorbed into sidecar memory. Never buffers history: events before
    /// the first reader attaches occupy the same bounded capacity.
    /// </summary>
    public int EventChannelCapacity { get; init; } = 1024;

    /// <summary>
    /// Optional observer of every raw wire line, both directions — the
    /// record–replay seam for the RPC contract goldens (#139). Outbound
    /// lines are reported at send attempt, under the stdin lock and before
    /// the write, so a fast answer can never be observed ahead of its own
    /// question; inbound lines are reported before demux, exactly as
    /// decoded by <see cref="JsonlCodec"/> (no trailing newline either
    /// way). Invoked synchronously on the send/pump paths,
    /// so implementations must be fast and must not throw — a throwing tap
    /// is connection-fatal for the child, same as any pump failure.
    /// </summary>
    public Action<PiWireDirection, string>? WireTap { get; init; }
}
