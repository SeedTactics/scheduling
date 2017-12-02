# SeedTactic OrderLink Scheduling

[![Build Status](https://ci.appveyor.com/api/projects/status/8pyaxsavarmbj5vx?svg=true)](https://ci.appveyor.com/project/wuzzeb/seedscheduling)
[![NuGet Stats](https://img.shields.io/nuget/v/BlackMaple.SeedTactics.Scheduling.svg)](https://www.nuget.org/packages/BlackMaple.SeedTactics.Scheduling)

This repository contains the scheduling plugin API for [OrderLink](https://www.seedtactics.com/products/seedtactic-orderlink).
A scheduling plugin takes the unscheduled bookings and the flexibility plan and produces the daily schedule to send
to the cell controller. For an overview of this process, see the [whitepaper](https://www.seedtactics.com/guide/orders-erp-automation).

To implement a custom scheduling strategy, create a .NET assembly which references
[BlackMaple.SeedTactics.Scheduling](https://www.nuget.org/packages/BlackMaple.SeedTactics.Scheduling/) from NuGet.
Then implement the `IAllocateInterface` interface in a class with a default no-parameter constructor.
OrderLink will search the .NET assembly for any class which implements the
interface and create a new instance of this type.
