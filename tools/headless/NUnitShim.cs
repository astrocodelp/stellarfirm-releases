// Minimal NUnit-compatible shim for the headless mono runner. Implements the
// exact subset of NUnit the EditMode suites use. NOT a replacement for the
// real Unity Test Framework run in CI; this is a fast local logic check.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)] public sealed class TestAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class SetUpAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class TearDownAttribute : Attribute { }

    public sealed class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    public static class Assert
    {
        public static void IsTrue(bool b, string msg = null)
        {
            if (!b) throw new AssertionException("Expected true. " + msg);
        }
        public static void IsFalse(bool b, string msg = null)
        {
            if (b) throw new AssertionException("Expected false. " + msg);
        }
        public static void IsNull(object o, string msg = null)
        {
            if (o != null) throw new AssertionException("Expected null, got " + o + ". " + msg);
        }
        public static void IsNotNull(object o, string msg = null)
        {
            if (o == null) throw new AssertionException("Expected not null. " + msg);
        }
        public static void IsNotEmpty(string s, string msg = null)
        {
            if (string.IsNullOrEmpty(s)) throw new AssertionException("Expected non-empty string. " + msg);
        }
        public static void AreEqual(object expected, object actual, string msg = null)
        {
            if (!Equals(expected, actual))
                throw new AssertionException("Expected <" + expected + "> but was <" + actual + ">. " + msg);
        }
        public static void AreEqual(float expected, float actual, float delta, string msg = null)
        {
            if (Math.Abs(expected - actual) > delta)
                throw new AssertionException("Expected <" + expected + "> +/- <" + delta + "> but was <" + actual + ">. " + msg);
        }
        public static void AreNotEqual(object notExpected, object actual, string msg = null)
        {
            if (Equals(notExpected, actual))
                throw new AssertionException("Expected any value except <" + notExpected + ">. " + msg);
        }
        public static void Greater(float actual, float floor, string msg = null)
        {
            if (!(actual > floor)) throw new AssertionException("Expected " + actual + " > " + floor + ". " + msg);
        }
        public static void GreaterOrEqual(float actual, float floor, string msg = null)
        {
            if (!(actual >= floor)) throw new AssertionException("Expected " + actual + " >= " + floor + ". " + msg);
        }
        public static void Less(float actual, float ceiling, string msg = null)
        {
            if (!(actual < ceiling)) throw new AssertionException("Expected " + actual + " < " + ceiling + ". " + msg);
        }
        public static void LessOrEqual(float actual, float ceiling, string msg = null)
        {
            if (!(actual <= ceiling)) throw new AssertionException("Expected " + actual + " <= " + ceiling + ". " + msg);
        }
        public static void Throws<T>(Action action, string msg = null) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            catch (Exception e)
            {
                throw new AssertionException("Expected " + typeof(T).Name + " but got " + e.GetType().Name + ". " + msg);
            }
            throw new AssertionException("Expected " + typeof(T).Name + " but nothing was thrown. " + msg);
        }
    }

    public static class CollectionAssert
    {
        public static void AreEqual(IEnumerable expected, IEnumerable actual, string msg = null)
        {
            var e = expected.Cast<object>().ToList();
            var a = actual.Cast<object>().ToList();
            if (e.Count != a.Count || !e.SequenceEqual(a))
                throw new AssertionException("Collections differ: [" + string.Join(",", e) + "] vs [" + string.Join(",", a) + "]. " + msg);
        }
        public static void Contains(object actual, object item, string msg = null)
        {
            Contains((IEnumerable)actual, item, msg);
        }
        public static void Contains(IEnumerable collection, object item, string msg = null)
        {
            foreach (var o in collection)
                if (Equals(o, item)) return;
            throw new AssertionException("Collection does not contain <" + item + ">. " + msg);
        }
    }
}

namespace Emberfall.Headless
{
    public static class Runner
    {
        public static int Main(string[] args)
        {
            int passed = 0, failed = 0;
            var failures = new List<string>();
            var asm = Assembly.GetExecutingAssembly();
            foreach (var type in asm.GetTypes().OrderBy(t => t.FullName))
            {
                var tests = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null)
                    .OrderBy(m => m.Name).ToList();
                if (tests.Count == 0) continue;
                var setUp = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<NUnit.Framework.SetUpAttribute>() != null);
                var tearDown = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<NUnit.Framework.TearDownAttribute>() != null);
                foreach (var test in tests)
                {
                    object instance = Activator.CreateInstance(type);
                    string name = type.Name + "." + test.Name;
                    try
                    {
                        if (setUp != null) setUp.Invoke(instance, null);
                        test.Invoke(instance, null);
                        passed++;
                        Console.WriteLine("PASS " + name);
                    }
                    catch (Exception e)
                    {
                        failed++;
                        var inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                        failures.Add(name + ": " + inner.Message);
                        Console.WriteLine("FAIL " + name + " :: " + inner.Message);
                    }
                    finally
                    {
                        try { if (tearDown != null) tearDown.Invoke(instance, null); } catch { }
                    }
                }
            }
            Console.WriteLine();
            Console.WriteLine(passed + " passed, " + failed + " failed (" + (passed + failed) + " total)");
            if (failures.Count > 0)
            {
                Console.WriteLine("\nFailures:");
                foreach (var f in failures) Console.WriteLine("  " + f);
            }
            return failed == 0 ? 0 : 1;
        }
    }
}
