namespace Project_Spacey.Programmer.Core.Planner
{
    public static class WeightBasePicker
    {
        public static long PickSeries(Dictionary<long, int> weights, Func<long, bool> allowed) {

            var pool = weights.Where(kv => allowed(kv.Key)).ToList();
            if (pool.Count == 0) return 0;

            var total = pool.Sum(kv => kv.Value);
            var r = Random.Shared.Next(1, total + 1);
            var acc = 0;

            foreach(var (sid , w) in pool)
            {
                acc += w;
                if (r <= acc) return sid;
            }
            return pool[0].Key;
        }

    }
}
