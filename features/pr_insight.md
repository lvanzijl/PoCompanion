I am looking to add a feature to give me insights about the PRs. There are complains about PRs being open too long, but also about PRs that are simply too big.

I'd like to retrieve data about PRs: time it's open, the timeline which shows the amount of iterations (with comments, when they are resolved etc. It should add from who the PR originates and the title. It should also retrieve information about how many files where hit and the average of lines per file was changed. Let's start with a feature description for GitHub copilot agent to retrieve this data and also add this data in the mock test data.

There is already a filter to configure from which goals the data should be taken, that configuration should be application wide instead of just for the workitems page.

All strategies about caching of data should be the same as the workitem explorer.

the user should see a user interface where you can see data about the pr's. be able to filter on iteration path or group by iteration path. group by user. do it per pr iteration (somehow take into account that comments lead to rework so the basic open time is flawed in that way). I also want to see amount of files/changes per pr because that gives insight if time open correlated to amount of files/changes etc.  would be cool if the UX was really simple with graphs instead of data tables, let's see what you can figure out for this