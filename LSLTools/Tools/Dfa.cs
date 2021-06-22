﻿// Decompiled with JetBrains decompiler
// Type: Tools.Dfa
// Assembly: Tools, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 7664DE95-CB1F-45A9-9E49-805BE209CFAA
// Assembly location: F:\Developer\radegast\Radegast\assemblies\Tools.dll

using System;
using System.Collections;

namespace Tools
{
  public class Dfa : LNode
  {
    public Hashtable m_map = new Hashtable();
    public string m_tokClass = "";
    public int m_reswds = -1;
    internal NList m_nfa = new NList();
    private YyLexer m_tokens;
    public Dfa.Action m_actions;

    private Dfa()
    {
    }

    public Dfa(TokensGen tks)
      : base(tks)
    {
      this.m_tokens = tks.m_tokens;
    }

    public Dfa(Nfa nfa)
      : base(nfa.m_tks)
    {
      this.m_tokens = this.m_tks.m_tokens;
      this.AddNfaNode((NfaNode) nfa);
      this.Closure();
      this.AddActions();
    }

    public static void SetTokens(YyLexer tks, Hashtable h)
    {
      foreach (Dfa dfa in (IEnumerable) h.Values)
      {
        if (dfa.m_tokens == null)
        {
          dfa.m_tokens = tks;
          Dfa.SetTokens(tks, dfa.m_map);
        }
      }
    }

    private void AddAction(int act)
    {
      this.m_actions = new Dfa.Action(act, this.m_actions);
    }

    private void MakeLastAction(int act)
    {
      while (this.m_actions != null && this.m_actions.a_act >= act)
        this.m_actions = this.m_actions.a_next;
      this.AddAction(act);
    }

    internal bool AddNfaNode(NfaNode nfa)
    {
      if (!this.m_nfa.Add(nfa))
        return false;
      if (nfa.m_sTerminal != "")
      {
        int length = 0;
        string str1 = "";
        string sTerminal = nfa.m_sTerminal;
        if (sTerminal[0] == '%')
        {
          length = 0;
          int index = 1;
          while (index < sTerminal.Length && (sTerminal[index] != ' ' && sTerminal[index] != '\t') && (sTerminal[index] != '\n' && sTerminal[index] != '{' && sTerminal[index] != ':'))
          {
            ++index;
            ++length;
          }
          str1 = nfa.m_sTerminal.Substring(1, length);
        }
        if (length > 0 && length + 1 < sTerminal.Length)
        {
          string str2 = nfa.m_sTerminal.Substring(length + 1).Trim();
          if (str2.Length > 0 && str2.StartsWith("%except"))
          {
            this.m_reswds = nfa.m_state;
            this.m_tks.m_tokens.reswds[(object) nfa.m_state] = (object) ResWds.New(this.m_tks, str2.Substring(7));
          }
        }
        if (str1 == "")
        {
          if (this.m_tokClass == "" || this.m_actions.a_act > nfa.m_state)
            this.AddAction(nfa.m_state);
        }
        else if (this.m_actions == null || this.m_actions.a_act > nfa.m_state)
        {
          this.MakeLastAction(nfa.m_state);
          this.m_tokClass = str1;
        }
      }
      return true;
    }

    internal void AddActions()
    {
      this.m_tks.states.Add((object) this);
      foreach (Charset charset in (IEnumerable) this.m_tks.m_tokens.cats.Values)
      {
        foreach (char key in (IEnumerable) charset.m_chars.Keys)
        {
          Dfa dfa = this.Target(key);
          if (dfa != null)
            this.m_map[(object) key] = (object) dfa;
        }
      }
    }

    internal Dfa Target(char ch)
    {
      Dfa dfa = new Dfa(this.m_tks);
      for (NList nlist = this.m_nfa; !nlist.AtEnd; nlist = nlist.m_next)
        nlist.m_node.AddTarget(ch, dfa);
      if (dfa.m_nfa.AtEnd)
        return (Dfa) null;
      dfa.Closure();
      for (int index = 0; index < this.m_tks.states.Count; ++index)
      {
        if (((Dfa) this.m_tks.states[index]).SameAs(dfa))
          return (Dfa) this.m_tks.states[index];
      }
      dfa.AddActions();
      return dfa;
    }

    private void Closure()
    {
      for (NList nlist = this.m_nfa; !nlist.AtEnd; nlist = nlist.m_next)
        this.ClosureAdd(nlist.m_node);
    }

    private void ClosureAdd(NfaNode nfa)
    {
      for (int index = 0; index < nfa.m_eps.Count; ++index)
      {
        NfaNode ep = (NfaNode) nfa.m_eps[index];
        if (this.AddNfaNode(ep))
          this.ClosureAdd(ep);
      }
    }

    internal bool SameAs(Dfa dfa)
    {
      NList nlist1 = this.m_nfa;
      NList nlist2;
      for (nlist2 = dfa.m_nfa; nlist1.m_node == nlist2.m_node && !nlist1.AtEnd; nlist2 = nlist2.m_next)
        nlist1 = nlist1.m_next;
      return nlist1.m_node == nlist2.m_node;
    }

    public int Match(string str, int ix, ref int action)
    {
      Dfa dfa;
      int num;
      if (ix < str.Length && (dfa = (Dfa) this.m_map[(object) this.m_tokens.Filter(str[ix])]) != null && (num = dfa.Match(str, ix + 1, ref action)) >= 0)
        return num + 1;
      if (this.m_actions == null)
        return -1;
      action = this.m_actions.a_act;
      return 0;
    }

    public void Print()
    {
      Console.Write("{0}:", (object) this.m_state);
      if (this.m_actions != null)
      {
        Console.Write(" (");
        for (Dfa.Action action = this.m_actions; action != null; action = action.a_next)
          Console.Write("{0} <", (object) action.a_act);
        if (this.m_tokClass != "")
          Console.Write(this.m_tokClass);
        Console.Write(">)");
      }
      Console.WriteLine();
      Hashtable hashtable = new Hashtable();
      IDictionaryEnumerator enumerator1 = this.m_map.GetEnumerator();
      int count = this.m_map.Count;
      while (count-- > 0)
      {
        enumerator1.MoveNext();
        char key1 = (char) enumerator1.Key;
        Dfa dfa1 = (Dfa) enumerator1.Value;
        if (!hashtable.Contains((object) key1))
        {
          hashtable[(object) key1] = (object) true;
          Console.Write("  {0}  ", (object) dfa1.m_state);
          int num1 = (int) key1;
          if (num1 >= 32 && num1 < 128)
            Console.Write(key1);
          else
            Console.Write(" #{0} ", (object) num1);
          IDictionaryEnumerator enumerator2 = this.m_map.GetEnumerator();
          do
          {
            enumerator2.MoveNext();
          }
          while ((Dfa) enumerator2.Value != dfa1);
          for (int index = count; index > 0; --index)
          {
            enumerator2.MoveNext();
            char key2 = (char) enumerator2.Key;
            Dfa dfa2 = (Dfa) enumerator2.Value;
            if (dfa1 == dfa2)
            {
              hashtable[(object) key2] = (object) true;
              int num2 = (int) key2;
              if (num2 >= 32 && num2 < 128)
                Console.Write(key2);
              else
                Console.Write(" #{0} ", (object) num2);
            }
          }
          Console.WriteLine();
        }
      }
    }

    public static object Serialise(object o, Serialiser s)
    {
      if (s == null)
        return (object) new Dfa();
      Dfa dfa = (Dfa) o;
      if (s.Encode)
      {
        s.Serialise((object) dfa.m_state);
        s.Serialise((object) dfa.m_map);
        s.Serialise((object) dfa.m_actions);
        s.Serialise((object) dfa.m_tokClass);
        s.Serialise((object) dfa.m_reswds);
        return (object) null;
      }
      dfa.m_state = (int) s.Deserialise();
      dfa.m_map = (Hashtable) s.Deserialise();
      dfa.m_actions = (Dfa.Action) s.Deserialise();
      dfa.m_tokClass = (string) s.Deserialise();
      dfa.m_reswds = (int) s.Deserialise();
      return (object) dfa;
    }

    public class Action
    {
      public int a_act;
      public Dfa.Action a_next;

      public Action(int act, Dfa.Action next)
      {
        this.a_act = act;
        this.a_next = next;
      }

      private Action()
      {
      }

      public static object Serialise(object o, Serialiser s)
      {
        if (s == null)
          return (object) new Dfa.Action();
        Dfa.Action action = (Dfa.Action) o;
        if (s.Encode)
        {
          s.Serialise((object) action.a_act);
          s.Serialise((object) action.a_next);
          return (object) null;
        }
        action.a_act = (int) s.Deserialise();
        action.a_next = (Dfa.Action) s.Deserialise();
        return (object) action;
      }
    }
  }
}
