# ResponseSchemaHeader

ASP.NET Core API middleware that allows client to specify what part of the response they need, and cuts out everything else before it's sent to the client.

## Installation

Get it from NuGet: [![NuGet](https://img.shields.io/nuget/vpre/ResponseSchemaHeader.svg?label=NuGet)](https://www.nuget.org/packages/ResponseSchemaHeader/)

## Features

- One line initialization
- Supports object and array responses
- Supports nested properties
- Supported response content types:
  - JSON

## Initialization

To start using the middleware just add `UseResponseSchemaHeader` into `Statup.cs` before `UseEndpoints`:

```cs
public void Configure(IApplicationBuilder app)
{
    app.UseResponseSchemaHeader();
}
```

## Usage

Now your client is able to add `ResponseSchema` header to any request, and ASP.NET will remove any properties that are not listed there. If client needs everything to be returned, `ResponseSchema` header can be omitted.

`ResponseSchema` should have following structure:

```json
[ 
    "topLevelProperty1", 
    "topLevelProperty2", 
    {
        "topLevelProperty3": [
            "nestedProperty1"
        ] 
    } 
]
```

If all nested properties from `topLevelProperty3` need to be included, add it as another string array item.

For example, let's say your action returns following information:

```json
[
    {
        "registration": "AA07AMM",
        "modelCode": "nissan-note",
        "color": "Turquoise",
        "year": 2007,
        "vehicleModel": {
            "code": "nissan-note",
            "manufacturerCode": "nissan",
            "name": "NOTE",
            "manufacturer": {
                "code": "nissan",
                "name": "NISSAN"
            }
        }
    },
    {
        "registration": "AAC792H",
        "modelCode": "hyundai-i10",
        "color": "Silver",
        "year": 1975,
        "vehicleModel": {
            "code": "volkswagen-up",
            "manufacturerCode": "volkswagen",
            "name": "UP",
            "manufacturer": {
                "code": "volkswagen",
                "name": "VOLKSWAGEN"
            }
        }
    },
```

And you only need `registration` and `vehicleModel.code` on your client.
After adding this header to the request (it's case insensitive by default):

```json
ResponseSchema: [ "Registration", "ModelCode", { "vehicleModel": [ "code" ] } ]
```

Response will be:

```json
[
    {
        "registration": "AA07AMM",
        "vehicleModel": {
            "code": "nissan-note"
        }
    },
    {
        "registration": "AAY452D",
        "vehicleModel": {
            "code": "volkswagen-up"
        }
    }
]
```
