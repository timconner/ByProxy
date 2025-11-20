namespace ByProxy.Models {
    public interface IOrderable {
        public int Order { get; set; }
    }

    public static class OrderableExtensions {
        public static IOrderedEnumerable<T> Ordered<T>(this IEnumerable<T> enumerable) where T : IOrderable {
            return enumerable.OrderBy(_ => _.Order);
        }

        public static IQueryable<T> Ordered<T>(this IQueryable<T> queryable) where T : IOrderable {
            return queryable.OrderBy(_ => _.Order);
        }

        public static void SortByOrder<T>(this List<T> list) where T : IOrderable {
            list.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        public static int MaxOrder<T>(this IEnumerable<T> enumerable) where T : IOrderable {
            if (!enumerable.Any()) return 0;
            return enumerable.Max(_ => _.Order);
        }

        public static bool MoveToTop<T>(this IEnumerable<T> enumerable, T target) where T : IOrderable {
            if (target.Order == 1) return false;

            target.Order = 0;
            enumerable.NormalizeOrderProperties();
            return true;
        }

        public static bool MoveItemTo<T>(this IEnumerable<T> enumerable, T target, int position) where T : IOrderable {
            if (target.Order == position) return false;

            target.Order = 0;
            int order = 1;
            foreach (var orderable in enumerable.Ordered().Skip(1)) {
                if (order == position) order++;
                orderable.Order = order;
                order++;
            }

            target.Order = position;
            enumerable.NormalizeOrderProperties();
            return true;
        }

        public static bool MoveAfter<T>(this IEnumerable<T> enumerable, T target, int position) where T : IOrderable {
            if (position < target.Order) position++;
            if (target.Order == position) return false;

            target.Order = 0;
            int order = 1;
            foreach (var orderable in enumerable.Ordered().Skip(1)) {
                if (order == position) order++;
                orderable.Order = order;
                order++;
            }

            target.Order = position;
            enumerable.NormalizeOrderProperties();
            return true;
        }

        public static bool MoveToBottom<T>(this IEnumerable<T> enumerable, T target) where T : IOrderable {
            int max = enumerable.MaxOrder();
            if (target.Order == max) return false;

            target.Order = max + 1;
            enumerable.NormalizeOrderProperties();
            return true;
        }

        public static bool MoveUp<T>(this IEnumerable<T> enumerable, T target) where T : IOrderable {
            var swapWith = enumerable
                .Ordered()
                .Reverse()
                .FirstOrDefault(_ => _.Order < target.Order);

            if (swapWith == null) return false;

            SwapOrderablePositions(target, swapWith);
            enumerable.NormalizeOrderProperties();
            return true;
        }

        public static bool MoveDown<T>(this IEnumerable<T> enumerable, T target) where T : IOrderable {
            var swapWith = enumerable
                .Ordered()
                .FirstOrDefault(_ => _.Order > target.Order);

            if (swapWith == null) return false;

            SwapOrderablePositions(target, swapWith);
            enumerable.NormalizeOrderProperties();
            return true;
        }

        public static int AppendOrderable<T>(this List<T> enumerable, T target) where T : IOrderable {
            enumerable.NormalizeOrderProperties();
            target.Order = enumerable.MaxOrder() + 1;
            enumerable.Add(target);
            return target.Order;
        }

        public static bool RemoveOrderable<T>(this List<T> enumerable, T target) where T : IOrderable {
            if (!enumerable.Contains(target)) return false;

            enumerable.Remove(target);
            enumerable.NormalizeOrderProperties();
            return true;
        }

        private static void SwapOrderablePositions(IOrderable a, IOrderable b) {
            int orderA = a.Order;
            a.Order = b.Order;
            b.Order = orderA;
        }

        public static void NormalizeOrderProperties<T>(this IEnumerable<T> enumerable) where T : IOrderable {
            int order = 1;
            foreach (var orderable in enumerable.Ordered()) {
                orderable.Order = order;
                order++;
            }
        }

        public static Dictionary<string, T> ToStringSortableDictionary<T>(this IEnumerable<T> enumerable) where T : IOrderable {
            enumerable.NormalizeOrderProperties();
            int width = enumerable.MaxOrder().ToString().Length;
            return enumerable
                .OrderBy(o => o.Order)
                .ToDictionary(
                    o => o.Order.ToString($"D{width}"),
                    o => o
                );
        }
    }
}
