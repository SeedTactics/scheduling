# %%
import numpy as nd
import pandas as pd
import functools
import allocate
import matplotlib.pyplot as plt

# %%
# Generate 30 days of orders using the Poisson distribution with the given rates
order_rates = [
    ("aaa", 400/30),
    ("bbb", 500/30),
    ("ccc", 500/30),
    ("xxx", 800/30),
    ("yyy", 800/30),
    ("zzz", 750/30)
]
order_days = 30
bookings = pd.concat(
    [
        pd.DataFrame({
            "BookingId": [part + "-" + str(i) for i in range(0,order_days)],
            "Priority": 100,
            "Part": part,
            "Quantity": pd.Series(nd.random.poisson(lam=lam, size=order_days)),
            "DueDate": pd.date_range(start="2018-01-15", periods=order_days)
        })
        for (part,lam) in order_rates
    ]
)
prev_parts = []

fig, ax = plt.subplots()
for label, df in bookings.groupby("Part"):
    df.plot(ax=ax, x="DueDate", y="Quantity", label=label)
plt.show()


# %%
results, simstat, simprod = allocate.allocate(
    bookings=bookings,
    prev_parts=prev_parts,
    flex_file="sample-flex.json",
    plugin="../../orderlink/lib/pegboard/allocate")
allocate.print_result_summary(results)
allocate.plot_simprod(results)
allocate.plot_simstat(simstat)


# %%
allocate.download(results, "localhost:5000")

