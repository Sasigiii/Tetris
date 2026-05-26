public interface IPoolable
{
    string PoolKey { get; }
    void OnPoolGet();
    void OnPoolRelease();
}
