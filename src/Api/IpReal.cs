using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.HttpOverrides;

namespace Dayspot.Api;

/// <summary>IP do cliente atrás da Cloudflare e do balanceador do Render, para os limites por IP.</summary>
public static class IpReal
{
    public static IServiceCollection AddIpReal(this IServiceCollection servicos)
    {
        // No Render o tráfego chega pela Cloudflare, que grava CF-Connecting-IP em toda
        // requisição e sobrescreve o que o cliente mandou. O X-Forwarded-For não serve:
        // ela acrescenta à lista, e o cliente controla o começo dela.
        servicos.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            o.ForwardedForHeaderName = "CF-Connecting-IP";
            o.ForwardLimit = 1;
            // O par da conexão é o balanceador do Render, sem faixa de IP publicada.
            o.KnownProxies.Clear();
            o.KnownIPNetworks.Clear();
        });
        return servicos;
    }

    /// <summary>Chave do cliente para contar limite: o IP real, com IPv6 agrupado por /64.</summary>
    public static string Chave(HttpContext ctx) => ChaveDoIp(ctx.Connection.RemoteIpAddress);

    // IPv6 conta por /64: é o bloco de um assinante, que troca de endereço dentro dele à vontade.
    private static string ChaveDoIp(IPAddress? ip)
    {
        if (ip is null)
        {
            return "";
        }
        if (ip.IsIPv4MappedToIPv6)
        {
            return ip.MapToIPv4().ToString();
        }
        if (ip.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return ip.ToString();
        }
        var bytes = ip.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes).ToString();
    }
}
