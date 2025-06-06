namespace TradeFlex.Core;

public abstract class BaseAlgorithm
{
    public virtual void OnStart(IContext context) { }
    public abstract void OnTick(Tick tick);
    public abstract void OnOrderFilled(Order order);
    public abstract void OnExit();
}

public interface IContext { }
public record Tick(decimal Price);
public record Order(string Symbol, int Quantity, decimal Price);
