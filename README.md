# SeedTactic OrderLink Scheduling

[![NuGet Stats](https://img.shields.io/nuget/v/BlackMaple.SeedTactics.Scheduling.svg)](https://www.nuget.org/packages/BlackMaple.SeedTactics.Scheduling)

This repository contains the scheduling plugin API for [OrderLink](https://www.seedtactics.com/features#seedtactic-orderlink).
A scheduling plugin takes the unscheduled bookings and the flexibility plan and produces the daily schedule to send
to the cell controller. For an overview of this process, see the [whitepaper](https://www.seedtactics.com/docs/tactics/orders-erp-automation).

A plugin is any executable which reads the `BlackMaple.SeedTactics.Scheduling.AllocateRequest`
formatted as JSON on standard input and writes a `BlackMaple.FMSInsight.API.NewJobs` formatted
as JSON on standard output. If using C#, the
[BlackMaple.SeedTactics.Scheduling](https://www.nuget.org/packages/BlackMaple.SeedTactics.Scheduling/) NuGet
can be used. Other languages could also be used as long as the JSON formats are correctly serialized/deserialized.
