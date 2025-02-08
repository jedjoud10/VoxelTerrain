using System;

public class Hashinator {
    public int hash;

    public Hashinator() {
        this.hash = 0;
    }

    public void Hash(object val) {
        hash = HashCode.Combine(hash, val.GetHashCode());
    }
}