right now there are some rules defined in the docs/ files, these don't come out of best practise so I'd like to verify them.

when we scale up there are two storages, one at the server side which holds settings and state that should be persisted even when a user moves to another workstation and the other one is settings that are temporary or should just only be saved for the current session. the PAT is one of the last onesz this shouldn't reside on some server somewhere where it could be hacked.

ignore the rules about pat storing in the docs/ files and make new ones with best practises and that what is written here. if there is something that I said that I'd potentially stupid just tell me so we can figure out the best way together.

when implemented replace the current pat storing rules with the new ones.
