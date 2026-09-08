using System.Collections.Generic;
using Xunit;

namespace MaxMind.Db.Test
{
    public static class CollectionActivatorTest
    {
        [Fact]
        public static void ListFactoryUsesRequestedCapacity()
        {
            var factory = new ListActivatorCreator().GetActivator(typeof(ICollection<long>));
            var list = Assert.IsType<List<long>>(factory(123));
            Assert.Empty(list);
            Assert.Equal(123, list.Capacity);
        }

        [Fact]
        public static void ListFactoryPreservesCustomDefaultConstructor()
        {
            var factory = new ListActivatorCreator().GetActivator(typeof(DefaultList<long>));
            var list = Assert.IsType<DefaultList<long>>(factory(123));
            Assert.True(list.WasConstructed);
            Assert.Equal(0, list.Capacity);
        }

        [Fact]
        public static void DictionaryFactoryCreatesRequestedInterface()
        {
            var factory = new DictionaryActivatorCreator().GetActivator(typeof(IDictionary<string, long>));
            var dictionary = Assert.IsType<Dictionary<string, long>>(factory(123));
            Assert.Empty(dictionary);
            dictionary.Add("value", 7);
            Assert.Equal(7, dictionary["value"]);
        }

        [Fact]
        public static void DictionaryFactoryPreservesCustomDefaultConstructor()
        {
            var factory = new DictionaryActivatorCreator().GetActivator(typeof(DefaultDictionary<string, long>));
            var dictionary = Assert.IsType<DefaultDictionary<string, long>>(factory(123));
            Assert.True(dictionary.WasConstructed);
            Assert.Empty(dictionary);
        }

        private sealed class DefaultList<T> : List<T>
        {
            public DefaultList()
            {
                WasConstructed = true;
            }

            public bool WasConstructed { get; }
        }

        private sealed class DefaultDictionary<TKey, TValue> : Dictionary<TKey, TValue> where TKey : notnull
        {
            public DefaultDictionary()
            {
                WasConstructed = true;
            }

            public bool WasConstructed { get; }
        }
    }
}
