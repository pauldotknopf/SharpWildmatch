﻿// ReSharper disable InconsistentNaming

namespace SharpWildmatch
{
    public static class Wildmatch
    {
        private static MatchResult DoWild(CharPointer p, CharPointer text, MatchFlags flags)
        {
            char p_ch;
            var pattern = p;
            for ( ; (p_ch = p.Value) != '\0'; text = text.Increment(), p = p.Increment())
            {
                int matched;
                char t_ch;

                if ((t_ch = text.Value) == '\0' && p_ch != '*')
                    return MatchResult.AbortAll;
                
                if (flags.HasFlag(MatchFlags.CaseFold))
                {
                    if (char.IsUpper(t_ch))
                        t_ch = char.ToLower(t_ch);
                    if (char.IsUpper(p_ch))
                        p_ch = char.ToLower(p_ch);
                }
                
                switch (p_ch)
                {
                    case '\\':
                        p = p.Increment();
                        p_ch = p.Value;
                        // fallthrough
                        goto default;
                    default:
                        if (t_ch != p_ch)
                            return MatchResult.NoMatch;
                        continue;
                    case '?':
                        if (flags.HasFlag(MatchFlags.PathName) && t_ch == '/')
                            return MatchResult.NoMatch;
                        continue;
                    case '*':
                        p = p.Increment();
                        bool match_slash;
                        if (p.Value == '*')
                        {
                            var prev_p = p.Increment(-2);

                            p = p.Increment();
                            while (p.Value == '*') p.Increment();

                            if (!flags.HasFlag(MatchFlags.PathName))
                            {
                                match_slash = true;
                            }
                            else if((prev_p.Index < pattern.Index || prev_p.Value == '/') &&
                                    (p.Value == '\0' || p.Value == '/' ||
                                     (p.Value == '\\' && p.Increment().Value == '/')))
                            {
                                if (p.Value == '/' &&
                                    DoWild(p.Increment(), text, flags) == MatchResult.Match)
                                    return MatchResult.Match;
                                match_slash = true;
                            }
                            else
                            {
                                return MatchResult.AbortMalformed;
                            }
                        }
                        else
                        {
                            match_slash = !flags.HasFlag(MatchFlags.PathName);
                        }
                        
                        if (p.Value == '\0') {
                            /* Trailing "**" matches everything.  Trailing "*" matches
                             * only if there are no more slash characters. */
                            if (!match_slash)
                            {
                                if (text.Source.Substring(text.Index).Contains("/"))
                                    return MatchResult.NoMatch;
                            }
                            return (int)MatchResult.Match;
                        } else if (!match_slash && p.Value == '/') {
                            var nextIndex = text.Source.Substring(text.Index).IndexOf('/');
                            if (nextIndex == -1)
                            {
                                return MatchResult.NoMatch;
                            }
                            text = text.Increment(nextIndex);
                            break;
                        }

                        while (true)
                        {
                            if(t_ch == '\0')
                                break;

                            if (!Sane.IsGlobSpecial(p.Value))
                            {
                                p_ch = p.Value;
                                if ((flags.HasFlag(MatchFlags.CaseFold)) && char.IsUpper(p_ch))
                                    p_ch = char.ToLower(p_ch);

                                while ((t_ch = text.Value) != '\0' &&
                                       (match_slash || t_ch != '/')) {
                                    if (flags.HasFlag(MatchFlags.CaseFold) && char.IsUpper(t_ch))
                                        t_ch = char.ToLower(t_ch);
                                    if (t_ch == p_ch)
                                        break;
                                    text = text.Increment();
                                }
                                if (t_ch != p_ch)
                                    return MatchResult.NoMatch;
                            }
                            
                            if ((matched = (int)DoWild(p, text, flags)) != (int)MatchResult.NoMatch) {
                                if (!match_slash || matched != (int)MatchResult.AbortToStartStart)
                                    return (MatchResult)matched;
                            } else if (!match_slash && t_ch == '/')
                                return MatchResult.AbortToStartStart;

                            text = text.Increment();
                            t_ch = text.Value;
                        }
                        
                        return MatchResult.AbortAll;
                    case '[':
                        p = p.Increment();
                        p_ch = p.Value;

                        if (p_ch == '^')
                            p_ch = '!';
                        
                        var negated = p_ch == '!' ? 1 : 0;
                        if (negated == 1)
                        {
                            p = p.Increment();
                            p_ch = p.Value;
                        }
                        
                        var prev_ch = '\0';
                        matched = 0;

                        bool Next()
                        {
                            prev_ch = p_ch;
                            p = p.Increment();
                            p_ch = p.Value;
                            return p_ch != ']';
                        }

                        do
                        {
                            if (p_ch == '\0')
                            {
                                return MatchResult.AbortAll;
                            }

                            if (p_ch == '\\')
                            {
                                p = p.Increment();
                                p_ch = p.Value;
                                if (p_ch == '\0')
                                    return MatchResult.AbortAll;
                                if (t_ch == p_ch)
                                    matched = 1;
                            } ﻿else if (p_ch == '-' && prev_ch != '\0' && p.Increment().Value != '\0' && p.Increment().Value != ']')
                            {
                                p = p.Increment();
                                p_ch = p.Value;
                                if (p_ch == '\\')
                                {
                                    p = p.Increment();
                                    p_ch = p.Value;
                                    if (p_ch == '\0')
                                        return MatchResult.AbortAll;
                                }
                                if (t_ch <= p_ch && t_ch >= prev_ch)
                                    matched = 1;
                                else if (flags.HasFlag(MatchFlags.CaseFold) && char.IsLower(t_ch)) {
                                    var t_ch_upper = char.ToUpper(t_ch);
                                    if (t_ch_upper <= p_ch && t_ch_upper >= prev_ch)
                                        matched = 1;
                                }

                                p_ch = '\0';
                            } ﻿else if (p_ch == '[' && p.Increment().Value == ':')
                            {
                                CharPointer s;
                                for (s = p.Increment(2); (p_ch = p.Value) != '\0' && p_ch != ']'; p = p.Increment()) { }
                                
                                if (p_ch == '\0')
                                    return MatchResult.AbortAll;
                                
                                var i = p.Index - s.Index - 1;
                                if (i < 0 || p.Increment(-1).Value != ':')
                                {
                                    p = s.Increment(-2);
                                    p_ch = '[';
                                    if (t_ch == p_ch)
                                        matched = 1;
                                    continue;
                                }
                                
                                var temp = s.Source.Substring(s.Index);
                                if (temp.StartsWith("alnum"))
                                {
                                    if (Sane.IsAlNum(t_ch))
                                        matched = 1;
                                }
                                else if (temp.StartsWith("alpha"))
                                {
                                    if (Sane.IsAlpha(t_ch))
                                        matched = 1;
                                }
                                else if (temp.StartsWith("blank"))
                                {
                                    if (Sane.IsBlank(t_ch))
                                        matched = 1;
                                }
                                else if (temp.StartsWith("cntrl"))
                                {
                                    if (Sane.IsCtrl(t_ch))
                                        matched = 1;
                                }
                                else if (temp.StartsWith("digit"))
                                {
                                    if (Sane.IsDigit(t_ch))
                                        matched = 1;
                                }
                                else if (temp.StartsWith("graph"))
                                {
                                    if (Sane.IsGraph(t_ch))
                                        matched = 1;
                                }
                                else if (temp.StartsWith("lower"))
                                {
                                    if (char.IsLower(t_ch))
                                        matched = 1;
                                }
                                else if (temp.StartsWith("print"))
                                {
                                    if (Sane.IsPrint(t_ch))
                                        matched = 1;
                                }
                                else if (temp.StartsWith("punct"))
                                {
                                    if (Sane.IsPunc(t_ch))
                                        matched = 1;
                                }
                                else if (temp.StartsWith("space"))
                                {
                                    if (Sane.IsSpace(t_ch))
                                        matched = 1;
                                }
                                else if (temp.StartsWith("upper"))
                                {
                                    if (char.IsUpper(t_ch))
                                    {
                                        matched = 1;
                                    }
                                    else if (flags.HasFlag(MatchFlags.CaseFold) && char.IsLower(t_ch))
                                    {
                                        matched = 1;
                                    }
                                }
                                else if (temp.StartsWith("xdigit"))
                                {
                                    if (Sane.IsXDigit(t_ch))
                                        matched = 1;
                                }
                                else
                                {
                                    return MatchResult.AbortAll;
                                }
                            } else if (t_ch == p_ch)
                            {
                                matched = 1;
                            }
                        } while (Next());
                        
                        if (matched == negated ||
                            (flags.HasFlag(MatchFlags.PathName) && t_ch == '/'))
                            return MatchResult.NoMatch;
                        continue;
                }
            }

            return text.Index < text.Source.Length ? MatchResult.NoMatch : MatchResult.Match;
        }
        
        public static MatchResult Match(string pattern, string text, MatchFlags matchFlags)
        {
            return DoWild(pattern, text, matchFlags);
        }
    }
}