# App.Flurl

![AppyWay logo](resources/appyway-100x100.png)

## What is Appy.Flurl?

[Flurl](https://github.com/tmenier/Flurl) HTTP client library extensions for NETCore and NET 5.0.

## Json Serializers

| Package | Latest Stable |
| --- | --- |
| [Flurl.Serialization.TextJson](https://www.nuget.org/packages/Flurl.Serialization.TextJson) | [![Nuget Package](https://img.shields.io/badge/nuget-3.0.0-blue.svg)](https://www.nuget.org/packages/Flurl.Serialization.TextJson) |


## Table of Contents

- [Flurl System.Text.Json Serializer](#flurl-system.text.json-serializer)
    * [Installing](#installing)
    * [Usage](#usage)

## Flurl System.Text.Json Serializer

Flurl have a default JsonSerialization implementation registered globally using NewtonsoftJsonSerializer, but you can change it for any other implementation. 

### Installing

Install using the [Flurl.Serialization.TextJson NuGet package](https://www.nuget.org/packages/Flurl.Serialization.TextJson):

```
PM> Install-Package Flurl.Serialization.TextJson
```

### Usage

As explained in the Flurl [documentation](https://flurl.dev/docs/configuration/) there are different ways to setup the default JsonSerializer:

- Call once at application startup:

```
var jsonSettings = new JsonSerializerOptions 
{ 
    IgnoreNullValues = true 
};

FlurlHttp.Configure(settings => settings.WithTextJsonSerializer(jsonSettings));
```

- Configure using directly the FlurlClient

```
public class AppyApiClient
{
    readonly IFlurlClient _client;

    public AppyApiClient(HttpClient httpClient)
    {
        var jsonSettings = new JsonSerializerOptions 
        { 
            IgnoreNullValues = true 
        };

        _client = new FlurlClient(httpClient).Configure(settings => settings.WithTextJsonSerializer(jsonSettings));
    }

    public Task<GetCustomerByIdQueryResult> Execute(GetCustomerByIdQuery query, CancellationToken cancellationToken)
    {
        return _client
            .Request("queries/getCustomerById")
            .PostJsonAsync(query, cancellationToken)
            .ReceiveJson<GetCustomerByIdQueryResult>();
    }
}
```

## Contribute
It would be awesome if you would like to contribute code or help with bugs. Just follow the guidelines [CONTRIBUTING](https://github.com/YellowLineParking/Appy.Flurl/blob/master/CONTRIBUTING.md)