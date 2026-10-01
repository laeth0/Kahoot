namespace Kahoot.Api.UnitTests.TestSupport;

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

public sealed class TestRequestCookieCollection : IRequestCookieCollection
{
    private readonly Dictionary<string, string> _cookies;

    public TestRequestCookieCollection(IDictionary<string, string> cookies)
    {
        _cookies = new Dictionary<string, string>(cookies, System.StringComparer.OrdinalIgnoreCase);
    }

    public string? this[string key] => _cookies.TryGetValue(key, out string? value) ? value : null;

    public int Count => _cookies.Count;

    public ICollection<string> Keys => _cookies.Keys;

    public bool ContainsKey(string key) => _cookies.ContainsKey(key);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value) => _cookies.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _cookies.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _cookies.GetEnumerator();
}
