// テスト実行用の NUnit 最小互換（Tools/CompileCheck/run.sh から使う）
using System; using System.Linq; using System.Reflection;
namespace NUnit.Framework {
  public class TestAttribute : Attribute {}
  public class AssertionException : Exception { public AssertionException(string m):base(m){} }
  public static class Assert {
    static void F(string m){ throw new AssertionException(m); }
    public static void AreEqual(object e, object a, string m=null){ if(!Equals(Convert.ChangeType(e, a?.GetType()??e.GetType()), a)) F($"expected {e} got {a} {m}"); }
    public static void AreNotEqual(object e, object a, string m=null){ if(Equals(e,a)) F($"not expected {e} {m}"); }
    public static void AreEqual(double e, double a, double delta, string m=null){ if(Math.Abs(e-a)>delta) F($"expected {e} got {a} {m}"); }
    public static T Throws<T>(Action act) where T: Exception { try { act(); } catch (T ex) { return ex; } F("expected exception " + typeof(T).Name); return null; }
    public static void AreSame(object e, object a, string m=null){ if(!ReferenceEquals(e,a)) F("not same "+m); }
    public static void Fail(string m){ F(m); }
    public static void IsTrue(bool c, string m=null){ if(!c) F("IsTrue "+m); }
    public static void Greater(IComparable a, IComparable b, string m=null){ if(a.CompareTo(Convert.ChangeType(b,a.GetType()))<=0) F($"{a}>{b} {m}"); }
    public static void GreaterOrEqual(IComparable a, IComparable b, string m=null){ if(a.CompareTo(Convert.ChangeType(b,a.GetType()))<0) F($"{a}>={b} {m}"); }
    public static void Less(IComparable a, IComparable b, string m=null){ if(a.CompareTo(Convert.ChangeType(b,a.GetType()))>=0) F($"{a}<{b} {m}"); }
  }
  public class Constraint { public Func<object,bool> f; }
  public static class Is { public static Constraint GreaterThan(int v)=>new Constraint{f=o=>Convert.ToInt32(o)>v}; }
  public static class Assume { public static void That(object o, Constraint c){ if(!c.f(o)) throw new Exception("inconclusive"); } }
}
class Runner { static int Main(){ int fail=0;
  foreach (var t in typeof(SoccerIsland.NPC.Tests.NPCSystemTests).Assembly.GetTypes())
   foreach (var m in t.GetMethods().Where(m=>m.GetCustomAttribute<NUnit.Framework.TestAttribute>()!=null)) {
     try { m.Invoke(Activator.CreateInstance(t), null); Console.WriteLine("PASS "+m.Name); }
     catch (TargetInvocationException e) { fail++; Console.WriteLine("FAIL "+m.Name+": "+e.InnerException.Message); } }
  return fail; } }
