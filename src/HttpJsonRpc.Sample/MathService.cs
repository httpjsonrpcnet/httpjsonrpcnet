namespace HttpJsonRpc.Sample
{
    public class MathService : IMathService
    {
        public int Sum(int n1, int n2)
        {
            return n1 + n2;
        }
    }
}