namespace CogniLabel.Application.Pipeline;

public interface IConcurrencyProvider
{
    int GetMaxConcurrency();
}

public sealed class DefaultConcurrencyProvider : IConcurrencyProvider
{
    public int GetMaxConcurrency() => Math.Max(1, Environment.ProcessorCount - 1);
}

